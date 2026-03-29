using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class RoomDataProfile
{
	[Header("基础信息")]
	public string roomName = "New Room";

	[Min(1)]
	public int sizeX = 1;

	[Min(1)]
	public int sizeY = 1;

	[Header("建造消耗")]
	public List<ResourceAmount> constructionCosts = new List<ResourceAmount>();

	[Header("每周期消耗")]
	public List<ResourceAmount> cycleCosts = new List<ResourceAmount>();

	[Header("生产设置")]
	[Min(0.1f)]
	public float cycleSeconds = 30f;
	public List<ResourceAmount> cycleOutputs = new List<ResourceAmount>();

	[Header("员工需求")]
	[Min(0)]
	public int requiredWorkers = 0;

	[Header("日均产出")]
	public bool useCustomDailyOutputs = false;
	public List<ResourceAmount> customDailyOutputs = new List<ResourceAmount>();

	public Vector2Int Size => new Vector2Int(Mathf.Max(1, sizeX), Mathf.Max(1, sizeY));

	public void Validate()
	{
		sizeX = Mathf.Max(1, sizeX);
		sizeY = Mathf.Max(1, sizeY);
		cycleSeconds = Mathf.Max(0.1f, cycleSeconds);
		requiredWorkers = Mathf.Max(0, requiredWorkers);

		constructionCosts = NormalizeResourceList(constructionCosts);
		cycleCosts = NormalizeResourceList(cycleCosts);
		cycleOutputs = NormalizeResourceList(cycleOutputs);
		customDailyOutputs = NormalizeResourceList(customDailyOutputs);
	}

	public List<ResourceAmount> BuildDailyOutputs(float daySeconds)
	{
		if (useCustomDailyOutputs)
		{
			return CloneResourceList(customDailyOutputs);
		}

		float safeDaySeconds = Mathf.Max(0f, daySeconds);
		float safeCycleSeconds = Mathf.Max(0.1f, cycleSeconds);
		float cyclesPerDay = safeDaySeconds / safeCycleSeconds;

		Dictionary<ResourceType, int> totals = new Dictionary<ResourceType, int>();
		for (int i = 0; i < cycleOutputs.Count; i++)
		{
			ResourceAmount output = cycleOutputs[i];
			if (output.amount <= 0)
			{
				continue;
			}

			int dailyAmount = Mathf.FloorToInt(output.amount * cyclesPerDay);
			if (dailyAmount <= 0)
			{
				continue;
			}

			if (totals.TryGetValue(output.type, out int existing))
			{
				totals[output.type] = existing + dailyAmount;
			}
			else
			{
				totals.Add(output.type, dailyAmount);
			}
		}

		List<ResourceAmount> result = new List<ResourceAmount>();
		foreach (KeyValuePair<ResourceType, int> kv in totals)
		{
			result.Add(new ResourceAmount { type = kv.Key, amount = kv.Value });
		}

		return result;
	}

	public RoomProductionPlan ToProductionPlan()
	{
		return new RoomProductionPlan
		{
			roomId = roomName,
			constructionCosts = CloneResourceList(constructionCosts),
			requiredSquirrels = Mathf.Max(0, requiredWorkers),
			cycleSeconds = Mathf.Max(0.1f, cycleSeconds),
			cycleCosts = CloneResourceList(cycleCosts),
			cycleOutputs = CloneResourceList(cycleOutputs),
		};
	}

	private static List<ResourceAmount> NormalizeResourceList(List<ResourceAmount> src)
	{
		if (src == null)
		{
			return new List<ResourceAmount>();
		}

		Dictionary<ResourceType, int> totals = new Dictionary<ResourceType, int>();
		for (int i = 0; i < src.Count; i++)
		{
			ResourceAmount amount = src[i];
			if (amount.amount <= 0)
			{
				continue;
			}

			if (totals.TryGetValue(amount.type, out int existing))
			{
				totals[amount.type] = existing + amount.amount;
			}
			else
			{
				totals.Add(amount.type, amount.amount);
			}
		}

		List<ResourceAmount> normalized = new List<ResourceAmount>(totals.Count);
		foreach (KeyValuePair<ResourceType, int> kv in totals)
		{
			normalized.Add(new ResourceAmount { type = kv.Key, amount = kv.Value });
		}

		return normalized;
	}

	private static List<ResourceAmount> CloneResourceList(List<ResourceAmount> src)
	{
		if (src == null)
		{
			return new List<ResourceAmount>();
		}

		List<ResourceAmount> clone = new List<ResourceAmount>(src.Count);
		for (int i = 0; i < src.Count; i++)
		{
			clone.Add(src[i]);
		}

		return clone;
	}
}

public class Roommanager : MonoBehaviour
{
	[Header("一天时长(秒)，用于自动计算日均产出")]
	[Min(1f)]
	public float daySeconds = 300f;

	[Header("房间数据配置")]
	public List<RoomDataProfile> roomProfiles = new List<RoomDataProfile>();

	private readonly Dictionary<string, RoomDataProfile> _profileByName = new Dictionary<string, RoomDataProfile>();

	public IReadOnlyList<RoomDataProfile> Profiles => roomProfiles;

	private void Awake()
	{
		RebuildLookup();
	}

	private void OnValidate()
	{
		daySeconds = Mathf.Max(1f, daySeconds);
		ValidateProfiles();
		RebuildLookup();
	}

	public bool TryGetProfile(string roomName, out RoomDataProfile profile)
	{
		profile = null;
		if (string.IsNullOrWhiteSpace(roomName))
		{
			return false;
		}

		if (_profileByName.Count != roomProfiles.Count)
		{
			RebuildLookup();
		}

		return _profileByName.TryGetValue(roomName.Trim(), out profile);
	}

	public List<ResourceAmount> GetDailyOutputs(string roomName)
	{
		if (!TryGetProfile(roomName, out RoomDataProfile profile))
		{
			return new List<ResourceAmount>();
		}

		return profile.BuildDailyOutputs(daySeconds);
	}

	public bool TryApplyProductionPlan(string roomName, RoomProductionUnit targetUnit)
	{
		if (targetUnit == null)
		{
			return false;
		}

		if (!TryGetProfile(roomName, out RoomDataProfile profile))
		{
			return false;
		}

		targetUnit.ApplyPlan(profile.ToProductionPlan());
		return true;
	}

	private void ValidateProfiles()
	{
		if (roomProfiles == null)
		{
			roomProfiles = new List<RoomDataProfile>();
			return;
		}

		for (int i = 0; i < roomProfiles.Count; i++)
		{
			if (roomProfiles[i] == null)
			{
				roomProfiles[i] = new RoomDataProfile();
			}

			roomProfiles[i].Validate();
		}
	}

	private void RebuildLookup()
	{
		ValidateProfiles();

		_profileByName.Clear();
		for (int i = 0; i < roomProfiles.Count; i++)
		{
			RoomDataProfile profile = roomProfiles[i];
			if (profile == null || string.IsNullOrWhiteSpace(profile.roomName))
			{
				continue;
			}

			string key = profile.roomName.Trim();
			_profileByName[key] = profile;
		}
	}



}
