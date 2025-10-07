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
    [SerializeField] private Transform muzzlePoint;
    public GameObject target;
    public Camera cam;
    
    public GameObject agent;
    public GameObject gun;
    public TMPro.TMP_Text targetsText;
    public TMPro.TMP_Text accuracyText;
    public TMPro.TMP_Text shotsFiredText;
    public TMPro.TMP_Text episodeNumText;
    public TMPro.TMP_Text stepsText;
    public TMPro.TMP_Text rewardText;
    private List<Int64> activeTargets = new List<Int64>();
    private List<GameObject> targets = new List<GameObject>();
    [SerializeField]  LayerMask targetMask;
    
    private int shotsFired = 0;
    private int targetsHit = 0;
    private bool fired = false;
    private int episodeNum = 0;
    private bool shotGun = false;
    private int framesPassed = 0;
    private int steps = 0;
    private int currTarget = -10;
    private float episodeReward = 0;
    
    override public void Initialize()
    {
        target.layer = (int) Mathf.Log(targetMask.value, 2);
        // Instanciate targets
        for (int i = 0; i < numTargets; i++)
        {
            targets.Add(Instantiate(target));
            activeTargets.Add(1); // 1 means the corresponding target in targets is active, 0 inactive
            targets[i].transform.SetParent(this.transform);
            targets[i].layer = (int) Mathf.Log(targetMask.value, 2);
        }
        
        Academy.Instance.StatsRecorder.Add("Shooting/ShotsFired", shotsFired);
        Academy.Instance.StatsRecorder.Add("Performance/TargetsHit", targetsHit);

    }

    private void FixedUpdate()
    {

        if (shotGun && framesPassed == 5)
        {
            gun.transform.Rotate(0, 0, -45);
            shotGun = false;
        }
        
        RaycastHit hit;
        Ray camRay = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        Vector3 targetPoint;
        
        if (Physics.Raycast(camRay, out RaycastHit camHit, Mathf.Infinity, targetMask))
        {
            targetPoint = camHit.point; // Aim where camera ray hits
        }
        else
        {
            targetPoint = camRay.GetPoint(1000f); // Arbitrary far distance if nothing hit
        }

        // 3. Compute direction from muzzle to that point
        Vector3 origin = muzzlePoint.position;
        Vector3 direction = (targetPoint - origin).normalized;

        // 4. Shoot ray from muzzle
        if (Physics.Raycast(origin, direction, out hit, Mathf.Infinity, targetMask))
        {

            if (fired)
            {
                if (!shotGun)
                {
                    gun.transform.Rotate(0, 0, 45);
                    shotGun = true;
                    framesPassed = 0;
                }
                
                for (int i = 0; i < numTargets; i++)
                {
                    if (targets[i] == hit.collider.gameObject)
                    {
                        activeTargets[i] = 0;
                        targets[i].SetActive(false);
                        targets[i].GetComponent<Renderer>().material.color = Color.orange;
                    }
                }
                AddReward(100f);
                episodeReward += 100f;
                shotsFired++;
                targetsHit++;
            }
            else
            {
                AddReward(0.1f);
                episodeReward += (0.1f);
            }
        }
        else
        {
            if (fired)
            {
                AddReward(-0.25f);
                episodeReward += -0.25f;
                shotsFired++;
            }
            
            // choose only one target, until it is destroyed, instead of moving between targets.

            if (currTarget == -10)
            {
                
            } else if (currTarget == -1 || activeTargets[currTarget] == 0)
            {
                float minAngle = float.MaxValue;
                int minTarget = -1;
                // Move to the next target as main target
                for (int i = 0; i < numTargets; i++)
                {
                    if (activeTargets[i] == 1)
                    {
                        Vector3 tLoc = targets[i].transform.position - cam.transform.position;
                        Vector3 camLoc = cam.transform.forward;
                        float angleDelta = Vector3.Angle(tLoc, camLoc);
                        if (angleDelta < minAngle)
                        {
                            minAngle = angleDelta;
                            minTarget = i;
                        }
                    }
                }
                currTarget = minTarget;
                targets[minTarget].GetComponent<Renderer>().material.color = Color.red;
                // float aimReward = Mathf.Clamp01(1f - (minAngle / 90f));
                // AddReward(aimReward * 0.1f);
                // episodeReward += (aimReward * 0.1f);
            } 
            // else
            // {
            //     Vector3 tLoc = targets[currTarget].transform.position - cam.transform.position;
            //     Vector3 camLoc = cam.transform.forward;
            //     float angleDelta = Vector3.Angle(tLoc, camLoc);
            //
            //     float aimReward = Mathf.Clamp01(1f - (angleDelta / 90f));
            //     AddReward(aimReward * 0.1f);
            //     episodeReward += (aimReward * 0.1f);
            // }
            
        }

        framesPassed++;
        
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
            targets[i].transform.localPosition = new Vector3(Random.Range(-9.0f, 9.0f), Random.Range(1.0f, 9.0f), Random.Range(0.0f, 9.0f)); // assumign (x, y, z)
            activeTargets[i] = 1; // set to active
            targets[i].SetActive(true);
            targets[i].GetComponent<Renderer>().material.color = Color.orange;
        }
        
        // Reset the gun rotation
        cam.transform.localRotation = Quaternion.Euler(0, 0, 0);
        agent.transform.localRotation = Quaternion.Euler(0, 0, 0);
        
        targetsHit = 0;
        shotsFired = 0;
        steps = 0;
        fired = false;
        episodeNum++;
        episodeReward = 0;
        currTarget = -1;
        
        targetsText.text = "Targets Hit: " + targetsHit + "/" +  numTargets;
        accuracyText.text = "Episode Accuracy: " + "100%";
        shotsFiredText.text = "Shots Fired: " + shotsFired;
        episodeNumText.text = "Epsiode Number: " + episodeNum;
        stepsText.text = "Steps: " + steps;
        rewardText.text = "Reward: " + episodeReward;
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // We can either give the observation as vector sensor of each of the positions of the targets
        // Or we can have one target at a time and change the observation
        // Or we can use camera instead of vector observation.
        
        // Add the position of each of the targets
        for (int i = 0; i < numTargets; i++)
        {
            if (activeTargets[i] == 1)
            {
                sensor.AddObservation(targets[i].transform.localPosition.x / 9f);
                sensor.AddObservation((targets[i].transform.localPosition.y - 4.5f) / 4.5f);
                sensor.AddObservation((targets[i].transform.localPosition.z - 4.5f) / 4.5f);
                // Add the angle distance to currentActive target
                Vector3 targetDirection = targets[i].transform.position - cam.transform.position;
                Vector3 camForward = cam.transform.forward;
                Quaternion desiredRotation = Quaternion.LookRotation(targetDirection);
                Vector3 desiredEuler = desiredRotation.eulerAngles;
                Vector3 currentEuler = cam.transform.eulerAngles;
                float yawDelta = Mathf.DeltaAngle(currentEuler.y, desiredEuler.y);   // Y-axis (left/right)
                float pitchDelta = Mathf.DeltaAngle(currentEuler.x, desiredEuler.x); // X-axis (up/down)
                
                sensor.AddObservation(yawDelta / 180.0f);
                sensor.AddObservation(pitchDelta / 90.0f);
                sensor.AddObservation(1.0f); // activeTarget
            }
            else
            {
                sensor.AddObservation(0f);
                sensor.AddObservation(0f);
                sensor.AddObservation(0f);
                sensor.AddObservation(0f);
                sensor.AddObservation(0f);
                sensor.AddObservation(0f); // inactiveTarget
            }
        }

        if (currTarget != -1 && currTarget != -10)
        {
            // Add the angle distance to currentActive target
            Vector3 targetDirection = targets[currTarget].transform.position - cam.transform.position;
            Vector3 camForward = cam.transform.forward;
            Quaternion desiredRotation = Quaternion.LookRotation(targetDirection);
            Vector3 desiredEuler = desiredRotation.eulerAngles;
            Vector3 currentEuler = cam.transform.eulerAngles;
            float yawDelta = Mathf.DeltaAngle(currentEuler.y, desiredEuler.y);   // Y-axis (left/right)
            float pitchDelta = Mathf.DeltaAngle(currentEuler.x, desiredEuler.x); // X-axis (up/down)
            
            sensor.AddObservation(yawDelta / 180); // Normalized between -1 and 1
            sensor.AddObservation(pitchDelta / 90); // Normalized between -1 and 1
        }
        else
        {
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
        }
        
        // Add the rotation of the camera
        
        
        sensor.AddObservation((cam.transform.localRotation.eulerAngles.x - 180f) / 180f);
        sensor.AddObservation((cam.transform.localRotation.eulerAngles.y - 180f) / 180f);
        sensor.AddObservation((cam.transform.localRotation.eulerAngles.z - 180f) / 180f);
    }

    override public void OnActionReceived(ActionBuffers actionBuffers)
    {
        // Get the action for the Y-axis between -90 and +90
        // Get the action for Z-axis between -90 and +90
        // Discrete for shoot or not shoot
        
        float yRotation = 2f * Mathf.Clamp(actionBuffers.ContinuousActions[0], -1f, 1f); // How much to rotate the gun by in the Y-axis
        float xRotation = 2f * Mathf.Clamp(-actionBuffers.ContinuousActions[1], -1f, 1f); // How much to rotate the gun by in the Z-axis
        int fire = actionBuffers.DiscreteActions[0]; // Whether to shoot the gun, (0 means dont shoot, 1 means shoot)

        var currY = cam.transform.eulerAngles.y;
        var currX = cam.transform.eulerAngles.x;

        if ((currY + yRotation > 90f) && (currY + yRotation < 270f)) yRotation = 0;
        if  ((currX + xRotation > 30f) && (currX + xRotation < 300f)) xRotation = 0;
            
        cam.transform.Rotate(0, yRotation* Time.fixedDeltaTime * 60f, 0, Space.World);
        cam.transform.Rotate(xRotation * Time.fixedDeltaTime * 60f, 0, 0, Space.Self);
        
        if (targetsHit >= numTargets)
        {
            AddReward(1000f);
            episodeReward += 1000f;
            EndEpisode();
        }
        
        if (fire == 1)
        {
            fired = true;
        } else
        {
            fired = false;
        }
        
        targetsText.text = "Targets Hit: " + targetsHit + "/" +  numTargets;
        shotsFiredText.text = "Shots Fired: " + shotsFired;
        stepsText.text = "Steps: " + steps;
        rewardText.text = "Reward: " + episodeReward;
        if (shotsFired >= 1)
        {
            accuracyText.text = "Episode Accuracy: " + (double) targetsHit / shotsFired + "%";
        }

        steps++;
    }

    override public void Heuristic(in ActionBuffers actionsOut)
    {
        var discreteActionsOut = actionsOut.DiscreteActions;
        var continuousActionsOut = actionsOut.ContinuousActions;
        continuousActionsOut[0] = 10*Input.GetAxis("Mouse X");
        continuousActionsOut[1] = 10*Input.GetAxis("Mouse Y");
        
        if (Input.GetMouseButton(0))
        {
            discreteActionsOut[0] = 1;
        }
        else
        {
            discreteActionsOut[0] = 0;
        }
    }

}
