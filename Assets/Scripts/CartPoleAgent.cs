using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GeneticLib.Genome.NeuralGenomes;
using UnityEngine;
using UnityEngine.UI;

public class CartPoleAgent : MonoBehaviour
{
	public static int nbOfInputs = 4;

	public bool isAI = true;

    // The controll speed.
	public float xVelocity = 10f;

    // Values used for normalization.
	public float poleMaxAngVel = 130f;
	public float cartMaxXVel= 13;

	public Rigidbody2D cartRb;   
	public Rigidbody2D poleRb;
	private Vector3 initialPolePos;
	private SpriteRenderer poleSprite;

	[Header("Fitness calculations")]
	public float reward = 1;
	public float goodSolutionPart = 0.8f;
	private float startOfGoodSolution = -1;

	public Text fitnessDisplay;

	public NeuralGenome neuralGenome;

	private void Start()
	{
		initialPolePos = poleRb.transform.localPosition;
		poleSprite = poleRb.GetComponent<SpriteRenderer>();
	}

	private void FixedUpdate()
	{
		if (!RailwayDelimiteter.IsInsideLimits(cartRb.transform))
		{
			if (neuralGenome != null)
				neuralGenome.Fitness -= Mathf.Abs(neuralGenome.Fitness) / 3;
			PopulationProxy.instance.DeactivateAgent(this);
		}

		if (isAI)
		{
			MoveFromNetwork();
			neuralGenome.Fitness += ComputeFitnessForThisTick();

			float fitnessToDisplay = neuralGenome.Fitness + GetBonusScoreFromTime();
			fitnessDisplay.text = string.Format("{0:0.0}", fitnessToDisplay);
		}
		else
			MoveFromUserInput();
	}

	public void ResetAgent(NeuralGenome newNeuralGenome = null)
	{
		if (newNeuralGenome != null)
			neuralGenome = newNeuralGenome;

		gameObject.SetActive(true);

		cartRb.transform.localPosition = Vector2.zero;

		StopRb(cartRb);
		StopRb(poleRb);

		poleRb.transform.SetPositionAndRotation(initialPolePos,
		                                        Quaternion.Euler(new Vector3(0, 0, 180)));

		neuralGenome.Fitness = 0;      
		startOfGoodSolution = -1;
	}

	public void End()
	{
		if (startOfGoodSolution > 0)
		{
			neuralGenome.Fitness += GetBonusScoreFromTime();
			startOfGoodSolution = -1;
		}
	}

	private void MoveFromUserInput()
	{
		cartRb.AddForce(
			Vector2.right *
			Input.GetAxis("Horizontal") *
			xVelocity *
			Time.fixedDeltaTime);
	}

	private void MoveFromNetwork()
	{
		if (neuralGenome == null)
			return;

		neuralGenome.FeedNeuralNetwork(GenerateNetworkInputs());
		var outputs = neuralGenome.Outputs.Select(x => x.Value).ToArray();
		cartRb.AddForce(Vector2.right * outputs[0] * xVelocity * Time.fixedDeltaTime);
	}

	private float[] GenerateNetworkInputs()
	{
		var result = new[]
		{
			poleRb.transform.localRotation.z,
			poleRb.transform.localRotation.w,
			poleRb.angularVelocity / poleMaxAngVel,
			cartRb.velocity.x / cartMaxXVel
		};

		Debug.Assert(result.Length == nbOfInputs);

		return result;
	}

	private string ArrayToStr<T>(T[] array)
	{
		return string.Join(", ", array.Select(x => x.ToString()));
	}

	private float ComputeFitnessForThisTick()
	{
		var zAngles = Mathf.Abs(GetPoleRotation());
		var part = zAngles / 180f;

		if (part > goodSolutionPart)
		{
			var resultPart = Mathf.Pow(part, 4);
            poleSprite.color = Color.Lerp(Color.red, Color.green, resultPart);

			if (startOfGoodSolution < 0)
				startOfGoodSolution = Time.time;
		}
		else
		{
			if (startOfGoodSolution > 0)
			{
				var result = GetBonusScoreFromTime();
				startOfGoodSolution = -1;            
				return result;
			}
			poleSprite.color = Color.red;
		}

		return 0;
	}

	private float GetBonusScoreFromTime()
	{
		if (startOfGoodSolution > 0)
		{
			var result = 1 + Time.time - startOfGoodSolution;
			return Mathf.Pow(result, 3);
		}
		else         
			return 0;
	}

	private void StopRb(Rigidbody2D rb)
	{
		rb.Sleep();
		rb.isKinematic = true;
		rb.velocity = Vector3.zero;
		rb.angularVelocity = 0;
		rb.transform.localRotation = Quaternion.Euler(Vector3.zero);      
		rb.isKinematic = false;
	}

	private float GetPoleRotation()
	{
		var rot = poleRb.transform.localRotation.eulerAngles.z;
	    rot -= 180;      
		return rot;
	}
}
