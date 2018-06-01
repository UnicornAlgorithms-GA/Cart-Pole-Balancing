using UnityEngine;
using System.Collections;
using GeneticLib.Neurology;
using System;
using GeneticLib.Generations.InitialGeneration;
using GeneticLib.Genome;
using System.Collections.Generic;
using GeneticLib.Neurology.Neurons;
using System.Linq;
using GeneticLib.Genome.NeuralGenomes;
using GeneticLib.Genome.Genes;
using GeneticLib.Neurology.Synapses;

public class NIGCSimpleNetwork : NeuralInitialGenerationCreatorBase
{
	public int Inputs { get; set; }
    public int Outputs { get; set; }
    public int[] HiddenLayers { get; set; }
    public bool BiasEnabled { get; set; }

	public NIGCSimpleNetwork(
        SynapseInnovNbTracker synapseInnovNbTracker,
        int inputs,
        int outputs,
        int[] hiddenLayers,
        Func<float> randomWeightFunc,
        bool biasEnabled = true
    )
        : base(synapseInnovNbTracker, randomWeightFunc)
    {
        Inputs = inputs;
        Outputs = outputs;
        HiddenLayers = hiddenLayers;
        BiasEnabled = biasEnabled;
    }

    protected override IGenome NewRandomGenome()
    {
        var neuronInnov = 0;

        var inputNeurons = Enumerable.Range(0, Inputs)
                                     .Select(i => new InputNeuron(neuronInnov++))
                                     .ToList();

        var outputNeurons = Enumerable.Range(0, Outputs)
                                      .Select(i => new OutputNeuron(
			                              neuronInnov++,
			                              ActivationFunctions.TanH))
                                      .ToList();

		var biasNeuron = BiasEnabled ? new BiasNeuron(neuronInnov++) : null;

        var hiddenLayers = new List<Neuron[]>();
        foreach (var n in HiddenLayers)
        {
            var layer = Enumerable.Range(0, n)
                                  .Select(i => new Neuron(
				                      neuronInnov++,
				                      ActivationFunctions.TanH));
            hiddenLayers.Add(layer.ToArray());
        }

        var synapses = Connect(
            inputNeurons,
            outputNeurons,
            biasNeuron,
            hiddenLayers
        );

        var allNeurons = inputNeurons.Concat(hiddenLayers.SelectMany(x => x))
                                     .Concat(outputNeurons);
        if (biasNeuron != null)
            allNeurons = allNeurons.Append(biasNeuron);

        var genome = new NeuralGenome(
            allNeurons.ToArray(),
            synapses.Select(x => new NeuralGene(x.Clone())).ToArray());

        return genome;
    }

    private List<Synapse> Connect(
        IEnumerable<Neuron> inputs,
        IEnumerable<Neuron> outputs,
        BiasNeuron biasNeuron,
        List<Neuron[]> hiddenLayers)
    {
        var layers = new List<IEnumerable<Neuron>>();

        layers.Add(inputs);
        layers.AddRange(hiddenLayers);
        layers.Add(outputs);
        layers = layers.Where(x => x.Any()).ToList();

        var synapses = ConnectLayers(layers).ToList();

        if (biasNeuron != null)
        {
            synapses.AddRange(ConnectNeuronToLayer(biasNeuron, outputs));
            synapses.AddRange(
                hiddenLayers.Select(layer => ConnectNeuronToLayer(biasNeuron, layer))
                            .SelectMany(x => x));
        }

        return synapses;
    }

    private IEnumerable<int> GetNeuronInnovIter()
    {
        int innov = 0;

        while (true)
            yield return innov++;
    }
}
