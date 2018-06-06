using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GeneticLib.Generations;
using GeneticLib.GeneticManager;
using GeneticLib.Genome.NeuralGenomes;
using GeneticLib.GenomeFactory;
using GeneticLib.GenomeFactory.GenomeProducer;
using GeneticLib.GenomeFactory.GenomeProducer.Breeding;
using GeneticLib.GenomeFactory.GenomeProducer.Breeding.Crossover;
using GeneticLib.GenomeFactory.GenomeProducer.Selection;
using GeneticLib.GenomeFactory.GenomeProducer.Reinsertion;
using GeneticLib.GenomeFactory.Mutation;
using GeneticLib.GenomeFactory.Mutation.NeuralMutations;
using GeneticLib.Neurology;
using GeneticLib.Randomness;
using UnityEngine;
using GeneticLib.Generations.InitialGeneration;
using GeneticLib.Genome.NeuralGenomes.NetworkOperationBakers;
using GeneticLib.Neurology.Neurons;
using GeneticLib.Neurology.NeuralModels;
using GeneticLib.Neurology.NeuronValueModifiers;
using GeneticLib.Utils.NeuralUtils;
using GeneticLib.Utils.Graph;

public class PopulationProxy : MonoBehaviour
{
	private static PopulationProxy instance;
	public static PopulationProxy Instance
	{
		get
		{
			if (instance == null)
				return instance = FindObjectOfType<PopulationProxy>();
			else
				return instance;
		}
	}
    
	private static readonly string pyNeuralNetGraphDrawerPath =
		"./Submodules/MachineLearningPyGraphUtils/PyNeuralNetDrawer.py";
    private static readonly string pyFitnessGraphPath =
		"../Submodules/MachineLearningPyGraphUtils/DrawGraph.py";
	private NeuralNetDrawer neuralNetDrawer;

	// Unity stuff
	public GameObject agentPrefab;
	public CartPoleAgent[] agents;

	[Header("Configs")]
	public float lifeSpan = 10f;
	private float startTime;

	public bool printFitness = true;

	[Header("Genetics configurations")]
	public int genomesCount = 50;

	public float singleSynapseMutChance = 0.2f;
	public float singleSynapseMutValue = 3f;

	public float allSynapsesMutChance = 0.1f;
	public float allSynapsesMutChanceEach = 1f;
	public float allSynapsesMutValue = 1f;

	public float crossoverPart = 0.80f;
	public float reinsertionPart = 0.2f;

	public float dropoutValue = 0.1f;

    GeneticManagerClassic geneticManager;

	public PopulationProxy()
	{      
		NeuralGenomeToJSONExtension.distBetweenNodes *= 5;
        NeuralGenomeToJSONExtension.randomPosTries = 10;
        NeuralGenomeToJSONExtension.xPadding = 0.03f;
        NeuralGenomeToJSONExtension.yPadding = 0.03f;      
	}
    
	private void Start()
	{
		GARandomManager.Random = new RandomClassic(
			(int)DateTime.Now.Subtract(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds);
                  
		NeuralNetDrawer.pyGraphDrawerPath = pyNeuralNetGraphDrawerPath;
		NeuralNetDrawer.pyAssemblyCmd = "/usr/local/bin/python3";
        PyDrawGraph.pyGraphDrawerFilePath = pyFitnessGraphPath;
		//neuralNetDrawer = new NeuralNetDrawer(false);

		InitAgents();
        InitGenetics();
        AssignBrains();
		//DrawBestGenome();
	}

	private void FixedUpdate()
	{
		if (Time.time > startTime + lifeSpan)
			Evolve();
	}

	#region UnityStuff
	private void InitAgents()
	{
		agents = Enumerable.Range(0, genomesCount)
						   .Select(i =>
		{
			var agent = Instantiate(agentPrefab, transform).GetComponent<CartPoleAgent>();
			agent.transform.localPosition = Vector2.zero;
			return agent;
		}).ToArray();      
	}

	private void AssignBrains()
	{
		var genomes = geneticManager.GenerationManager
									.CurrentGeneration
									.Genomes
									.Select(x => x as NeuralGenome)
									.ToArray();
		
		UnityEngine.Debug.Assert(agents.Length == genomes.Count());

		foreach (var pair in agents.Zip(genomes, (agent, genome) => new {agent, genome}))
			pair.agent.ResetAgent(pair.genome);

		startTime = Time.time;
	}

	public void DeactivateAgent(CartPoleAgent agent)
	{
		agent.End();
		agent.gameObject.SetActive(false);
		if (!agents.Any(x => x.gameObject.activeSelf))
			Evolve();
	}
	#endregion

	#region Genetics
	private void InitGenetics()
	{
		var model = new NeuralModelBase();
		model.defaultWeightInitializer = () => GARandomManager.NextFloat(-1, 1);;

        model.WeightConstraints = new Tuple<float, float>(-20f, 20f);

        var bias = model.AddBiasNeuron();
        var layers = new List<Neuron[]>()
        {
		    model.AddInputNeurons(CartPoleAgent.nbOfInputs).ToArray(),

            model.AddNeurons(
                new Neuron(-1, ActivationFunctions.TanH)
                {
				    ValueModifiers = new[] { Dropout.DropoutFunc(dropoutValue) },
                },
                count: 5
            ).ToArray(),

            model.AddNeurons(
                new Neuron(-1, ActivationFunctions.TanH)
                {
				    ValueModifiers = new[] { Dropout.DropoutFunc(dropoutValue) },
                },
                count: 5
            ).ToArray(),

            model.AddOutputNeurons(
                1,
			    ActivationFunctions.TanH
            ).ToArray(),
        };

        model.ConnectLayers(layers);
        model.ConnectBias(bias, layers.Skip(1));
        
        var initialGenerationGenerator = new NeuralInitialGenerationCreatorBase(
            model,
            new RecursiveNetworkOpBaker());

		//var selection = new EliteSelection();
		var selection = new RouletteWheelSelection();
		var crossover = new OnePointCrossover(true);
		var breeding = new BreedingClassic(
			crossoverPart,
			1,
			selection,
			crossover,
			InitMutations()
		);

		var reinsertion = new ReinsertionFromSelection(reinsertionPart, 0, new EliteSelection());
		var producers = new IGenomeProducer[] { breeding, reinsertion };
		var genomeForge = new GenomeForge(producers);

		var generationManager = new GenerationManagerKeepLast();
		geneticManager = new GeneticManagerClassic(
			generationManager,
			initialGenerationGenerator,
			genomeForge,
			genomesCount
		);

		geneticManager.Init();
	}

	public void Evolve()
	{
		foreach (var agent in agents)
			agent.End();

		//DrawBestGenome();

		geneticManager.Evolve();
		AssignBrains();
	}

	private MutationManager InitMutations()
	{
		var result = new MutationManager();
		result.MutationEntries.Add(new MutationEntry(
			new SingleSynapseWeightMutation(() => singleSynapseMutValue),
			singleSynapseMutChance,
			EMutationType.Independent
		));

		result.MutationEntries.Add(new MutationEntry(
			new SingleSynapseWeightMutation(() => singleSynapseMutValue * 3),
			singleSynapseMutChance / 40,
			EMutationType.Independent
		));

		result.MutationEntries.Add(new MutationEntry(
			new AllSynapsesWeightMutation(
				() => allSynapsesMutValue,
				allSynapsesMutChanceEach),
			allSynapsesMutChance,
			EMutationType.Independent
		));

		return result;
	}

	private void DrawBestGenome()
    {
        var best = geneticManager.GenerationManager
                                 .CurrentGeneration
                                 .BestGenome as NeuralGenome;
        var str = best.ToJson(
            neuronRadius: 0.02f,
            maxWeight: 5,
            edgeWidth: 1f);

		neuralNetDrawer.QueueNeuralNetJson(str);
    }
	#endregion
}
