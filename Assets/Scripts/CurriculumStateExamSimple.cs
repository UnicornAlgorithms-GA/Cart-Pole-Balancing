using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class CurriculumStateExamSimple : CurriculumState
{
	public bool statePassed = false;
	public override bool StatePassed { get { return statePassed; }}

	public int requiredGenomesAliveAtTheEnd = 10;
	public float requiredFitnessSum = 100;
	public int requiredGenerations = 3;
	private int currentGenerationCount = 0;

	public override void RegisterNewStateEvents()
	{
		bool condition1 = false, condition2 = false;

		var agents = PopulationProxy.Instance.agents;
		var activeAgents = agents.Where(x => x.gameObject.activeSelf).ToArray();
		condition1 = (activeAgents.Length >= requiredGenomesAliveAtTheEnd);

		if (condition1)
		{
			var sumFitness = activeAgents.Take(requiredGenomesAliveAtTheEnd)
										 .Sum(x => x.neuralGenome.Fitness);
			condition2 = (sumFitness >= requiredFitnessSum);
		}

		if (condition1 && condition2)
		{
			currentGenerationCount++;
			if (currentGenerationCount >= requiredGenerations)
				statePassed = true;
		}
		else
			currentGenerationCount = 0;
	}

	public override void Init()
	{
		currentGenerationCount = 0;
	}
}
