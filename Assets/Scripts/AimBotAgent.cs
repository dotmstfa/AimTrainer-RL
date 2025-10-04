using System;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using System.Collections.Generic;
using Random = UnityEngine.Random;

public class AimBotAgent : Agent
{
    [SerializeField] public int numTargets;
    public GameObject target;
    public GameObject gun;
    public TMPro.TMP_Text targetsText;
    public TMPro.TMP_Text accuracyText;
    public TMPro.TMP_Text shotsFiredText;
    private List<Int64> activeTargets = new List<Int64>();
    private List<GameObject> targets = new List<GameObject>();

    private int shotsFired = 0;
    private int targetsHit = 0;
    private bool fired = false;
    
    override public void Initialize()
    {
        // First target
        targets.Add(target);
        activeTargets.Add(1);
        // Rest of targets
        for (int i = 0; i < numTargets-1; i++)
        {
            targets.Add(Instantiate(target));
            activeTargets.Add(1); // 1 means the corresponding target in targets is active, 0 inactive
        }
    }

    private void FixedUpdate()
    {
        if (fired)
        {
            RaycastHit hit;
            Vector3 origin = gun.transform.position;
            Vector3 direction = -gun.transform.right;
            
            if (Physics.Raycast(origin,
                    direction, out hit,
                    Mathf.Infinity, 1 << 6))
            {
                Debug.DrawRay(origin, direction * hit.distance, Color.green);
                Debug.Log("Did hit");
                // Add reward of +1f
                // Set the target to inactive in activeTargets
                Debug.Log(hit);
                for (int i = 0; i < numTargets; i++)
                {
                    if (targets[i] == hit.collider.gameObject)
                    {
                        activeTargets[i] = 0;
                        targets[i].SetActive(false);
                    }
                }
                // Give reward for hitting target
                AddReward(1.0f);
                shotsFired++;
                targetsHit++;
            }
            else
            {
                // Give negative reward for hittign target but missing
                Debug.DrawRay(origin, direction * 1000, Color.red);
                Debug.Log("Did not hit");
                AddReward(-0.5f);
                shotsFired++;
            }
        }
    }

    override public void OnEpisodeBegin()
    {
        // Get random number of targets
        // Place each of the targets in random postion within constraints.
        // 0.5 <= y <= 4.5
        // -9.5 <= x <= 1
        // -4.5 <= z <= 4.5
        for (int i = 0; i < numTargets; i++)
        {
            targets[i].transform.localPosition = new  Vector3(Random.Range(-9.5f, 1f), Random.Range(0.5f, 4.5f), Random.Range(-4.5f, 4.5f)); // assumign (x, y, z)
            activeTargets[i] = 1; // set to active
            targets[i].SetActive(true);
        }
        
        // Reset the gun rotation
        gun.transform.localRotation = Quaternion.Euler(0, 0, 0);

        targetsHit = 0;
        shotsFired = 0;
        fired = false;
        
        targetsText.text = "Targets Hit: " + targetsHit + "/" +  numTargets;
        accuracyText.text = "Episode Accuracy: " + "100%";
        shotsFiredText.text = "Shots Fired: " + shotsFired;
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // We can either give the observation as vector sensor of each of the positions of the targets
        // Or we can have one target at a time and change the observation
        // Or we can use camera instead of vector observation.
        
        // Add the position of each of the targets
        for (int i = 0; i < numTargets; i++)
        {
            sensor.AddObservation(targets[i].transform.localPosition);
            sensor.AddObservation(activeTargets[i]);
        }
        
        // Add the rotation of the gun
        sensor.AddObservation(gun.transform.localRotation.eulerAngles);
    }

    override public void OnActionReceived(ActionBuffers actionBuffers)
    {
        // Get the action for the Y-axis between -90 and +90
        // Get the action for Z-axis between -90 and +90
        // Discrete for shoot or not shoot
        
        float yRotation = actionBuffers.ContinuousActions[0]; // How much to rotate the gun by in the Y-axis
        float zRotation = -actionBuffers.ContinuousActions[1]; // How much to rotate the gun by in the Z-axis
        int fire = actionBuffers.DiscreteActions[0]; // Whether to shoot the gun, (0 means dont shoot, 1 means shoot)
        
        gun.transform.localRotation = gun.transform.localRotation * Quaternion.Euler(0, yRotation, zRotation); // assuming x, y, z
        
        if (targetsHit >= numTargets)
        {
            EndEpisode();
        }
        
        if (fire == 1)
        {
            fired = true;
        } else
        {
            fired = false;
            AddReward(-0.01f); // Add negative reward to incentivize getting shots right away
        }
        
        targetsText.text = "Targets Hit: " + targetsHit + "/" +  numTargets;
        shotsFiredText.text = "Shots Fired: " + shotsFired;
        if (shotsFired >= 1)
        {
            accuracyText.text = "Episode Accuracy: " + targetsHit / shotsFired + "%";
        }
    }

    override public void Heuristic(in ActionBuffers actionsOut)
    {
        var discreteActionsOut = actionsOut.DiscreteActions;
        var continuousActionsOut = actionsOut.ContinuousActions;
        continuousActionsOut[0] = Input.GetAxis("Horizontal");
        continuousActionsOut[1] = Input.GetAxis("Vertical");
        
        if (Input.GetKey(KeyCode.Space))
        {
            discreteActionsOut[0] = 1;
        }
        else
        {
            discreteActionsOut[0] = 0;
        }

    }
}
