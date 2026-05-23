using UnityEngine;
using UnityEngine.AI;

public interface IEntityNavBehavior
{
    void Tick(NavMeshAgent agent);
}
