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
using GeneticLib.GenomeFactory.GenomeProducer.Breeding.Selection;
using GeneticLib.GenomeFactory.GenomeProducer.Reinsertion;
using GeneticLib.GenomeFactory.Mutation;
using GeneticLib.GenomeFactory.Mutation.NeuralMutations;
using GeneticLib.Neurology;
using GeneticLib.Randomness;
using UnityEngine;

public class PopulationProxy : MonoBehaviour
{
	public static PopulationProxy instance;
    
	// Unity stuff
	public GameObject agentPrefab;
	public CartPoleAgent[] agents;

	[Header("Configs")]
	public float lifeSpan = 10f;
	private float startTime;

	[Header("Genetics configurations")]
	public int genomesCount = 50;

	public float singleSynapseMutChance = 0.2f;
	public float singleSynapseMutValue = 3f;

	public float allSynapsesMutChance = 0.1f;
	public float allSynapsesMutChanceEach = 1f;
	public float allSynapsesMutValue = 1f;

	public float crossoverPart = 0.80f;
	public float reinsertionPart = 0.2f;

    GeneticManagerClassic geneticManager;

	private void Awake()
	{
		instance = this;
	}

	private void Start()
	{
		GARandomManager.Random = new RandomClassic(
			(int)DateTime.Now.Subtract(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds);

		InitAgents();
		InitGenetics();
		AssignBrains();
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
		
		Debug.Assert(agents.Length == genomes.Count());

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
		var synapseTracker = new SynapseInnovNbTracker();

		var initialGenerationGenerator = new NIGCSimpleNetwork(
			   synapseTracker,
			CartPoleAgent.nbOfInputs,
			   1,
			   new[] { 7, 7 },
			   () => (float)GARandomManager.Random.NextDouble(-1, 1),
			   true
		   );

		var selection = new EliteSelection();
		var crossover = new OnePointCrossover(true);
		var breeding = new BreedingClassic(
			crossoverPart,
			1,
			selection,
			crossover,
			InitMutations()
		);

		var reinsertion = new EliteReinsertion(reinsertionPart, 0);
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
	#endregion
}
