using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;

// Component "Behavior Parameters"
// -------------------------------
//  "Vector Observation" (INPUTS)
//      "Space Size" = number of neural network inputs
//      "Stacked Vectors" = gives a sense of history
//  "Vector Action" (OUTPUTS)
//      "Space Size" = number of neural network outputs

//  base config.yaml
//  new  configuration.yaml
//  yaml training file commands - https://blogs.ubc.ca/cogs300/training-commands/
//  yaml training file specify using your custom one - https://forum.unity.com/threads/how-do-i-train-for-more-steps.929247/

public class AvoidHolesAgent : Agent
{
    public float Movespeed = 20.0f;    
    public int cntFloorX = 8;
    public int cntFloorZ = 6;
    public GameObject prefabFloor = null;
    public float PercentFloorHoles = 0.15f;    
    private Bounds bndFloor;
    private Bounds bndAgent;
    private GameObject Target = null;
    private float RayDownLength = 1.0f;

    public override void Initialize()
    {
        bndFloor = prefabFloor.GetComponent<Renderer>().bounds;
        bndAgent = this.GetComponent<Renderer>().bounds;
        Target = this.transform.parent.transform.Find("Target").gameObject;
        MakeFloor();
    }
    public override void OnEpisodeBegin()
    {        
        MakeFloor();
        Globals.Episode += 1;        
    }
    public override void OnActionReceived(float[] vectorAction)
    {
        this.transform.Translate(Vector3.right * vectorAction[0] * Movespeed * Time.deltaTime);
        this.transform.Translate(Vector3.forward * vectorAction[1] * Movespeed * Time.deltaTime);
                
        OffFloorCheck();
    }
    public override void CollectObservations(VectorSensor sensor)
    {
        //Note: these are the pieces of information I determined the agent needs to know (sense) to avoid holes and reach target

        //floor feelers                A ---- B     corners of the base of the agent
        //                              \      \
        //                               C ---- D
        RaycastHit hit;
        Vector3 Apos = new Vector3(bndAgent.min.x, this.transform.position.y, bndAgent.min.z);
        Vector3 Bpos = new Vector3(bndAgent.max.x, this.transform.position.y, bndAgent.min.z);
        Vector3 Cpos = new Vector3(bndAgent.min.x, this.transform.position.y, bndAgent.max.z);
        Vector3 Dpos = new Vector3(bndAgent.max.x, this.transform.position.y, bndAgent.max.z);
        bool A = Physics.Raycast(Apos, transform.TransformDirection(Vector3.down), out hit, RayDownLength);
        bool B = Physics.Raycast(Bpos, transform.TransformDirection(Vector3.down), out hit, RayDownLength);
        bool C = Physics.Raycast(Cpos, transform.TransformDirection(Vector3.down), out hit, RayDownLength);
        bool D = Physics.Raycast(Dpos, transform.TransformDirection(Vector3.down), out hit, RayDownLength);
        sensor.AddObservation(A);                           // +1
        sensor.AddObservation(B);                           // +1
        sensor.AddObservation(C);                           // +1
        sensor.AddObservation(D);                           // +1   
              
        //position of agent
        sensor.AddObservation(this.transform.position);     // +3

        //direction agent to target
        Vector3 direction = Vector3.Normalize(this.transform.position - Target.transform.position);
        sensor.AddObservation(direction);                   // +3

        //position of target
        sensor.AddObservation(Target.transform.position);   // +3   = 13 observations
    }
    public override void Heuristic(float[] actionsOut)
    {
        //Heuristic: is a good way to test you actions to see if they work as you would expect
        actionsOut[0] = 0;  //left right    
        actionsOut[1] = 0;  //forward backward

        if (Input.GetKey(KeyCode.LeftArrow) == true)
            actionsOut[0] = -1;
        if (Input.GetKey(KeyCode.RightArrow) == true)
            actionsOut[0] = 1;

        if (Input.GetKey(KeyCode.UpArrow) == true)
            actionsOut[1] = 1;
        if (Input.GetKey(KeyCode.DownArrow) == true)
            actionsOut[1] = -1;
    }
    private void MakeFloor()
    {
        //delete all floor pieces
        for (int i = this.transform.parent.childCount - 1; i >= 0; i--)
        {
            GameObject child = this.transform.parent.GetChild(i).gameObject;
            if (child.CompareTag("Floor") == true)
            {
                GameObject.Destroy(child);
            }
        }

        Vector3 offset = new Vector3(this.transform.parent.position.x, this.transform.parent.position.y, this.transform.parent.position.z);

        bool flgAgent = false;
        bool flgTarget = false;
        for (int x = 0; x < cntFloorX; x++)
        {
            for (int z = 0; z < cntFloorZ; z++)
            {
                if (Random.Range(0, 100) > PercentFloorHoles * 100)
                {
                    //create floor
                    GameObject obj = Instantiate(prefabFloor, this.transform.parent);
                    obj.name = string.Format("floor{0}_{1}", x, z);
                    obj.transform.position = new Vector3(offset.x + x * bndFloor.size.x, offset.y - 1, offset.z + z * bndFloor.size.z);
                                        
                    if (flgAgent == false && Random.Range(0,100) < 10)
                    {
                        //place agent
                        this.transform.position = new Vector3(offset.x + x * bndFloor.size.x, offset.y, offset.z + z * bndFloor.size.z);
                        flgAgent = true;
                    }
                    else if (flgTarget == false && Random.Range(0,100) < 10)
                    {
                        //place target
                        Target.transform.position = new Vector3(offset.x + x * bndFloor.size.x, offset.y, offset.z + z * bndFloor.size.z);
                        flgTarget = true;
                    }
                }
            }
        }
    }
    private void OffFloorCheck()
    {
        //Note: cast ray straight down to make sure gameobject is still over floor
        RaycastHit hit;
        if (Physics.Raycast(transform.position, transform.TransformDirection(Vector3.down), out hit, RayDownLength) == false)
        {
            if (this.transform.position.y <= 0) //ground level
            {
                //Fail
                AddReward(-0.1f);
                Globals.Fail += 1;
                Globals.ScreenText();
                EndEpisode();
            }                       
        }
        else
        {
            //like the agent to keep moving vs staying still
            AddReward(0.0001f);
        }
    }
    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Target") == true)
        {
            //Success - the goal of training
            Globals.Success += 1;
            AddReward(1.0f);
            Globals.ScreenText();
            EndEpisode();
        }
    }
}
