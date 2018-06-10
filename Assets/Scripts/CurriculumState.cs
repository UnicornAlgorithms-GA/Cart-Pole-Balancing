using UnityEngine;
using System.Collections;

public abstract class CurriculumState : MonoBehaviour
{
	public abstract bool StatePassed { get; }
	public abstract void Init();
	public abstract void RegisterNewStateEvents();
}
