using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class CurriculumLearningProxy : MonoBehaviour
{
	public static CurriculumLearningProxy instance;

	public Animator animator;

	public List<CurriculumState> curriculumStates;
	private int currentState = 1;

	private void Awake()
	{
		instance = this;
	}

	private void Start()
	{
		if (animator != null)
			animator = GetComponent<Animator>();
	}

	public void CheckForStateUpdate()
	{
		foreach (var state in curriculumStates)
			state.RegisterNewStateEvents();

		var passedState = curriculumStates.FirstOrDefault(
			x => x.StatePassed && curriculumStates.IndexOf(x) == currentState);

		if (passedState != null)
		{
			var index = curriculumStates.IndexOf(passedState) + 1;
			animator.Play("State" + index);
			Debug.Log("State" + index);
			currentState = index;

			if (curriculumStates.Count >= index)
				curriculumStates[index - 1].Init();
		}
	}
}
