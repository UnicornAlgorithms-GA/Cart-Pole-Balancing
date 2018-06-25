using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class CurriculumLearningProxy : MonoBehaviour
{
	public static CurriculumLearningProxy instance;

	public Animator animator;

	public List<CurriculumState> curriculumStates;
	private int currentState = 0;

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
		curriculumStates[currentState].RegisterNewStateEvents();

		if (curriculumStates[currentState].StatePassed)
		{
			if (currentState + 1 >= curriculumStates.Count())
				return;
			var index = currentState + 1;

			animator.Play("State" + (index + 1));
			animator.Update(0f);
			Debug.Log("State" + (index + 1));
			currentState = index;
            
			curriculumStates[index].Init();
		}
	}
}
