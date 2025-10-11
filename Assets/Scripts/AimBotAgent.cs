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
    [SerializeField] private int bulletNum = 20;
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
    
    private int currBullets;
    private int targetsHit = 0;
    private bool fired = false;
    private int episodeNum = 0;
    private bool shotGun = false;
    private int framesPassed = 0;
    private int steps = 0;
    private float episodeReward = 0;
    
    override public void Initialize()
    {
        currBullets = bulletNum;
        target.layer = (int) Mathf.Log(targetMask.value, 2);
        // Instanciate targets
        for (int i = 0; i < numTargets; i++)
        {
            targets.Add(Instantiate(target));
            activeTargets.Add(1); // 1 means the corresponding target in targets is active, 0 inactive
            targets[i].transform.SetParent(this.transform);
            targets[i].layer = (int) Mathf.Log(targetMask.value, 2);
        }
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
            targetPoint = camHit.point;
        }
        else
        {
            targetPoint = camRay.GetPoint(1000f);
        }
        
        Vector3 origin = muzzlePoint.position;
        Vector3 direction = (targetPoint - origin).normalized;
        
        if (Physics.Raycast(origin, direction, out hit, Mathf.Infinity, targetMask))
        {

            if (fired)
            {
                currBullets--;
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
                    }
                }
                AddReward(1f);
                episodeReward += 1f;
                targetsHit++;
            }
        } else
        {
            if (fired)
            {
                currBullets--;
                // Penalty for each missed bullet
                AddReward((float) -numTargets/bulletNum);
                episodeReward += (float) -numTargets/bulletNum;
            }
        }

        framesPassed++;
        
    }

    override public void OnEpisodeBegin()
    {
        for (int i = 0; i < numTargets; i++)
        {
            targets[i].transform.localPosition = new Vector3(Random.Range(-9.0f, 9.0f), Random.Range(1.0f, 9.0f), Random.Range(0.0f, 9.0f)); // assumign (x, y, z)
            activeTargets[i] = 1; // set to active
            targets[i].SetActive(true);
        }
        
        // Reset the gun rotation
        cam.transform.localRotation = Quaternion.Euler(0, 0, 0);
        agent.transform.localRotation = Quaternion.Euler(0, 0, 0);
        
        targetsHit = 0;
        steps = 0;
        fired = false;
        episodeNum++;
        episodeReward = 0;
        currBullets = bulletNum;
        
        targetsText.text = "Targets Hit: " + targetsHit + "/" +  numTargets;
        accuracyText.text = "Episode Accuracy: " + "100%";
        shotsFiredText.text = "Shots Fired: " + 0 + "/" + bulletNum;
        episodeNumText.text = "Epsiode Number: " + episodeNum;
        stepsText.text = "Steps: " + steps;
        rewardText.text = "Reward: " + episodeReward;
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        for (int i = 0; i < numTargets; i++)
        {
            if (activeTargets[i] == 1)
            {
                Vector3 targetDirection = targets[i].transform.position - cam.transform.position;
                Quaternion desiredRotation = Quaternion.LookRotation(targetDirection);
                Vector3 desiredEuler = desiredRotation.eulerAngles;
                Vector3 currentEuler = cam.transform.eulerAngles;
                float yawDelta = Mathf.DeltaAngle(currentEuler.y, desiredEuler.y);   // Y-axis (left/right)
                float pitchDelta = Mathf.DeltaAngle(currentEuler.x, desiredEuler.x); // X-axis (up/down)
                
                // Normalized yaw and pitch delta to target.
                sensor.AddObservation(yawDelta / 180.0f);
                sensor.AddObservation(pitchDelta / 90.0f);
                sensor.AddObservation(1.0f); // activeTarget
            }
            else
            {
                sensor.AddObservation(0f);
                sensor.AddObservation(0f);
                sensor.AddObservation(0f); // inactiveTarget
            }
        }
        
        // Normalized rotation of camera
        sensor.AddObservation((cam.transform.localRotation.eulerAngles.x - 180f) / 180f);
        sensor.AddObservation((cam.transform.localRotation.eulerAngles.y - 180f) / 180f);
        sensor.AddObservation((cam.transform.localRotation.eulerAngles.z - 180f) / 180f);
    }

    override public void OnActionReceived(ActionBuffers actionBuffers)
    {
        float yRotation = 2f * Mathf.Clamp(actionBuffers.ContinuousActions[0], -1f, 1f); // How much to rotate the gun by in the Y-axis
        float xRotation = 2f * Mathf.Clamp(-actionBuffers.ContinuousActions[1], -1f, 1f); // How much to rotate the gun by in the Z-axis
        int fire = actionBuffers.DiscreteActions[0]; // Whether to shoot the gun, (0 means dont shoot, 1 means shoot)

        var currY = cam.transform.eulerAngles.y;
        var currX = cam.transform.eulerAngles.x;

        if ((currY + yRotation > 90f) && (currY + yRotation < 270f)) yRotation = 0;
        if  ((currX + xRotation > 30f) && (currX + xRotation < 300f)) xRotation = 0;
            
        cam.transform.Rotate(0, yRotation* Time.fixedDeltaTime * 60f, 0, Space.World);
        cam.transform.Rotate(xRotation * Time.fixedDeltaTime * 60f, 0, 0, Space.Self);

        float accuracy = (float)targetsHit / (bulletNum - currBullets);
        if ((targetsHit >= numTargets) || (currBullets <= 0))
        {
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
        shotsFiredText.text = "Shots Fired: " + (bulletNum - currBullets) +  "/" + bulletNum;
        stepsText.text = "Steps: " + steps;
        rewardText.text = "Reward: " + episodeReward;
        accuracyText.text = "Accuracy: " + accuracy;
        steps++;
        
    }

    override public void Heuristic(in ActionBuffers actionsOut)
    {
        var discreteActionsOut = actionsOut.DiscreteActions;
        var continuousActionsOut = actionsOut.ContinuousActions;
        continuousActionsOut[0] = 10*Input.GetAxis("Mouse X");
        continuousActionsOut[1] = 10*Input.GetAxis("Mouse Y");
        
        if (Input.GetKeyDown(KeyCode.Space))
        {
            discreteActionsOut[0] = 1;
        }
        else
        {
            discreteActionsOut[0] = 0;
        }
    }

}
