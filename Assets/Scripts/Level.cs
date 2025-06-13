using System.Collections.Generic;
using UnityEngine;

public class Level : MonoBehaviour
{
    [SerializeField]
    private List<Cactus> cactusPrefabs = new List<Cactus>();

	[SerializeField]
	private Transform spawnPosition;

	[SerializeField]
	private Transform endPosition;

	[SerializeField]
	private float minSpawnDelay = 1f;

	[SerializeField]
	private float maxSpawnDelay = 2f;

	private List<Cactus> activeCactusses= new List<Cactus>();

	private float nextSpawnTime;


	private void Start()
	{
		SetNextSpawnTime();
	}

	private void SetNextSpawnTime()
	{
		nextSpawnTime = Time.time + GetRandomDelay();
	}

	private float GetRandomDelay() => UnityEngine.Random.Range(minSpawnDelay, maxSpawnDelay);

	private void Update()
	{
		MoveCactusses();

		if (nextSpawnTime > Time.time)
		{
			return;
		}

		SetNextSpawnTime();
		SpawnCactus();
	}

	private void MoveCactusses()
	{
		foreach (var cactus in activeCactusses)
		{
			cactus.Move();
		}

		var finishedCactusses = activeCactusses.FindAll(cac => cac.IsFinished());
		foreach (var finished in finishedCactusses)
		{
			activeCactusses.Remove(finished);
			Destroy(finished.gameObject);
		}
	}

	private void SpawnCactus()
	{
		var idx = UnityEngine.Random.Range(0, cactusPrefabs.Count);

		var cactus = cactusPrefabs[idx];

		var newCactus = Instantiate(cactus, spawnPosition.position,Quaternion.identity);
		newCactus.Setup(endPosition);
		activeCactusses.Add(newCactus);
	}
}
