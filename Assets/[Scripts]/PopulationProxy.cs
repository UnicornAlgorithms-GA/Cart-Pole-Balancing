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
using MoreLinq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Diagnostics;
using UnityEngine.UI;
using GeneticLib.Neurology.PredefinedStructures.LSTMs;

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
	private NeuralNetDrawer neuralNetDrawer = null;

	// Unity stuff
	public GameObject agentPrefab;
	public CartPoleAgent[] agents;

	[Header("Configs")]
	public float lifeSpan = 10f;
	private float startTime;
	public bool printFitness = true;
	public Text generationCounter;

	[Header("Random force")]
	public float force = 0.01f;
	public float forceInterval = 0.5f;
	public float forceMultiplier = 1;

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
	private GeneticManagerClassic geneticManager;

	[Header("Fitness calculations")]
	public float balanceRewardExp = 1.5f;
    public float centerReward = 0.3f;
    public float goodSolutionPart = 0.8f;

	[Header("General configs")]
	public float startingAngle = 0;
	public Vector3 startingPos = Vector3.zero;
	public float removeAgentIfBelowAnglePart = 0.6f;

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
		neuralNetDrawer = new NeuralNetDrawer(false);

		InitAgents();
        InitGenetics();
        AssignBrains();
		DrawBestGenome();
        
	}

	private void LateUpdate()
	{
		if (Mathf.RoundToInt(Time.time * 10) % 2 == 0)
			CameraFollowBest();
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
        var initialGenerationGenerator = new NeuralInitialGenerationCreatorBase(
			//Init4InputsNeuralModel(),
			Init2InputsNeuralModel(),
			new FeedForwardOpBaker());

		//var selection = new EliteSelection();
		//var selection = new RouletteWheelSelection();
		var selection = new RouletteWheelSelectionWithRepetion();

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
		CurriculumLearningProxy.instance.CheckForStateUpdate();
		foreach (var agent in agents)
			agent.End();

		DrawBestGenome();

		geneticManager.Evolve();
		AssignBrains();

		generationCounter.text = "Generation: " + geneticManager.GenerationNumber;
	}

	private INeuralModel Init2InputsNeuralModel()
	{
		var model = new NeuralModelBase();
        model.defaultWeightInitializer = () => GARandomManager.NextFloat(-1, 1); ;

        model.WeightConstraints = new Tuple<float, float>(-5f, 5f);

		var bias = model.AddBiasNeuron();
        var layers = new List<Neuron[]>()
        {
            model.AddInputNeurons(CartPoleAgent.nbOfInputs).ToArray(),

            model.AddOutputNeurons(
                1,
                ActivationFunctions.TanH
            ).ToArray(),
        };

        model.ConnectLayers(layers);

		var outputNeuron = layers.Last().Last();
		//var memNeurons = model.AddNeurons(
  //          sampleNeuron: new MemoryNeuron(-1, outputNeuron.InnovationNb),
  //          count: 1
		//).ToArray();

		// RNN

		//var innerMemNeurons = model.AddNeurons(
		//	sampleNeuron: new Neuron(-1, ActivationFunctions.TanH),
		//	count: 1).ToArray();

		//model.ConnectLayers(new[] { memNeurons, innerMemNeurons });
		//model.ConnectLayers(new[] { layers[0], innerMemNeurons, new[] { outputNeuron } });

		// LSTM
		Neuron lstmIn, lstmOut;
		model.AddLSTM(out lstmIn, out lstmOut, biasNeuron: bias);
		//model.ConnectNeurons(memNeurons, new[] { lstmIn }).ToArray();
		model.ConnectNeurons(layers[0], new[] { lstmIn }).ToArray();
		model.ConnectNeurons(new[] { lstmOut }, layers.Last()).ToArray();

		return model;
	}

	private INeuralModel Init4InputsNeuralModel()
	{
		var model = new NeuralModelBase();
        model.defaultWeightInitializer = () => GARandomManager.NextFloat(-1, 1); ;

        model.WeightConstraints = new Tuple<float, float>(-50f, 50f);

        var layers = new List<Neuron[]>()
        {
            model.AddInputNeurons(CartPoleAgent.nbOfInputs).ToArray(),         
            model.AddOutputNeurons(
                1,
                ActivationFunctions.TanH
            ).ToArray(),
        };

        model.ConnectLayers(layers);

		var outputNeuron = layers.Last().Last();
        model.AddConnection(outputNeuron.InnovationNb, outputNeuron.InnovationNb);

		return model;
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

	private void CameraFollowBest()
	{
		var active = agents.Where(x => x.gameObject.activeSelf).ToArray();
		if (active.Length == 0)
			return;
		var best = active.MaxBy(x => x.GetCurrentFitness());
		SmoothFollow.instance.target = best.cartRb.transform;
	}
}
