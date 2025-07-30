using Slither.Protocol;
using System.Collections.Generic;
using UnityEngine;

public class FoodManager : MonoBehaviour
{
    Dictionary<int, FoodContext> foods = new Dictionary<int, FoodContext>();
    public GameObject foodPrefab;

    public void ApplyFoodSpawn(FoodSpawnPayload payload)
    {
        DebugUtil.Dbg("[ApplyFoodSpawn] id : ", payload.id);
        Vector2 spawnPos = new Vector2(payload.x, payload.y);
        GameObject food = Instantiate(foodPrefab, spawnPos, Quaternion.identity);
        food.GetComponent<IdHolder>().SetId(payload.id);

        FoodContext context = new FoodContext { foodObject = food };
        foods[payload.id] = context;
    }

    public void ApplyFoodRemove(FoodRemovePayload payload)
    {
        DebugUtil.Dbg("[ApplyFoodRemove] id : ", payload.id);

        if (!foods.ContainsKey(payload.id))
        {
            Debug.LogWarning($"[ApplyFoodRemove] food id {payload.id} not found.");
            return;
        }
        Destroy(foods[payload.id].foodObject);
        foods.Remove(payload.id);
    }
}
