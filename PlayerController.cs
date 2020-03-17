using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using LepTimer;

public class PlayerController : MonoBehaviour
{
    //Ground
    public float groundSpeed = 3.44f;
    public float grAccel = 200f;

    //Air
    public float airSpeed = 3f;
    public float airAccel = 0.3f;
    public float airTargetSpeed = 8f;

    //Wall
    public float wallAccel = 5f;
    public float wallStickiness = 10f;
    public float wallStickDistance = 1f;
    public float wallRunTilt = 15f;

    //Jump
    public float jumpForce = 6f;
    public float dashForce = 4f;
    public float bhopLeniency = 0.1f;
    public float dJumpBaseSpd = 8;

    float bhopLenTimer = 0f;
    bool jump;
    bool canDJump = false;

    bool running = false;
    bool crouching = false;
    bool grounded = false;

    Vector3 groundNormal;

    float height;
    Vector3 dir;
    Vector3 spid;

    public HudTextScript spidHud;
    public HudTextScript spidUpHud;

    private enum State
    {
        Walking,
        Flying,
        Wallruning
    }
    private State state;

    public Collider wallRunCollider;
    private Rigidbody rb;
    private PhysicMaterial pm;
    private PlayerCameraController camCon;
    private GameObject wall = null;

    void Start()
    {

        camCon = transform.GetChild(0).GetComponent<PlayerCameraController>();
        rb = GetComponent<Rigidbody>();
        pm = GetComponent<CapsuleCollider>().material;
        height = GetComponent<Renderer>().bounds.size.y;
        camCon.SetTilt(0);

        EnterFlying();
        canDJump = !OnGround();
        pm.dynamicFriction = 0;
        pm.frictionCombine = PhysicMaterialCombine.Minimum;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetAxisRaw("Mouse ScrollWheel") != 0)
            jump = true;
        if (Input.GetKey(KeyCode.LeftShift) && Input.GetAxisRaw("Vertical") == 1)
            running = true;
        if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.C))
            crouching = true;
        if (Input.GetKeyDown(KeyCode.V))
        {
            rb.AddForce(dir.normalized * 20f, ForceMode.VelocityChange);
        }
        dir = Direction();
    }

    private void FixedUpdate()
    {
        if (rb.velocity.magnitude < 0.2f)
        {
            pm.dynamicFriction = 0.5f;
            pm.frictionCombine = PhysicMaterialCombine.Average;
        }
        else
        {
            pm.dynamicFriction = 0f;
            pm.frictionCombine = PhysicMaterialCombine.Minimum;
        }
        switch (state)
        {
            case State.Wallruning:
                camCon.SetTilt(WallrunCameraAngle() * wallRunTilt);
                Wallrun(dir, groundSpeed, grAccel);
                break;
            case State.Walking:
                camCon.SetTilt(0);
                if (crouching)
                    Crouch(dir, groundSpeed, grAccel);
                else
                    Walk(dir, groundSpeed, grAccel);
                break;
            case State.Flying:
                camCon.SetTilt(0);
                AirMove(dir, airSpeed, airAccel);
                break;
        }

        spidHud.SetValue(new Vector3(rb.velocity.x, 0, rb.velocity.z).magnitude);
        spidUpHud.SetValue(rb.velocity.y);

        if (bhopLenTimer >= 0f)
        {
            bhopLenTimer -= Time.deltaTime;
        }
        jump = false;
        running = false;
        crouching = false;
    }

    void OnCollisionStay(Collision collision)
    {
        if (collision.contactCount > 0)
        {
            float angle;
            foreach (ContactPoint contact in collision.contacts)
            {
                angle = Vector3.Angle(contact.normal, Vector3.up);
                
                if(angle < 40f)
                {
                    EnterWalking();
                    grounded = true;
                    groundNormal = contact.normal;
                    break;
                }
                grounded = false;
            }
            if (grounded == false)
            {
                foreach (ContactPoint contact in collision.contacts)
                {
                    if (contact.thisCollider == wallRunCollider && state != State.Walking)
                    {
                        angle = Vector3.Angle(contact.normal, Vector3.up);
                        if (angle > 40f && angle < 100f)
                        {
                            EnterWallrun();
                            groundNormal = contact.normal;
                            break;
                        }
                    }
                }
            }
        }
    }

    void OnCollisionExit(Collision collision)
    {
        if (collision.contactCount == 0)
        {
            EnterFlying();
        }
    }

    private Vector3 Direction()
    {
        float hAxis = Input.GetAxisRaw("Horizontal");
        float vAxis = Input.GetAxisRaw("Vertical");

        Vector3 direction = new Vector3(hAxis, 0, vAxis);
        return rb.transform.TransformDirection(direction);
    }

    private bool OnGround()
    {   //To be removed
        return grounded;
    }



    #region EnterState
    private void EnterWalking()
    {
        if (state != State.Walking)
        {
            bhopLenTimer = bhopLeniency;
            state = State.Walking;
        }
    }

    private void EnterFlying()
    {
        grounded = false;
        if (state != State.Flying)
        {
            wall = null;
            canDJump = true;
            state = State.Flying;
        }
    }

    private void EnterWallrun()
    {
        if (state != State.Wallruning)
        {
            canDJump = true;
            state = State.Wallruning;
            //This breaks jumping off the wall
            //Vector3 spid = rb.velocity;
            //spid = new Vector3(spid.x, 0, spid.z);
            //rb.velocity = spid;
        }
    }

    #endregion



    #region Movement

    private void Wallrun(Vector3 wishDir, float maxSpeed, float Acceleration)
    {
        wishDir = wishDir.normalized;
        wishDir = RotateToPlane(wishDir, groundNormal);
        rb.AddForce(-Physics.gravity);
        rb.AddForce(-groundNormal * wallStickiness, ForceMode.Acceleration);
        rb.AddForce(wishDir * wallAccel);
        if (jump)
        {
            Vector3 direction = new Vector3(groundNormal.x, 1, groundNormal.z);
            rb.AddForce(direction * jumpForce, ForceMode.Impulse);

            EnterFlying();
        }
    }

    private void AirMove(Vector3 wishDir, float maxSpeed, float Acceleration)
    {
        if (jump)
        {
            DoubleJump(wishDir);
        }
        if (wishDir.magnitude != 0)
        {
            Vector3 spid = new Vector3(rb.velocity.x, 0, rb.velocity.z);

            Vector3 projVel = Vector3.Project(rb.velocity, wishDir);

            bool isAway = Vector3.Dot(wishDir, projVel) <= 0f;

            if (projVel.magnitude < maxSpeed || isAway)
            {
                Vector3 vc = wishDir.normalized * Acceleration;

                if (!isAway)
                {
                    vc = Vector3.ClampMagnitude(vc, maxSpeed - projVel.magnitude);
                }
                else
                {
                    vc = Vector3.ClampMagnitude(vc, maxSpeed + projVel.magnitude);
                }
                Vector2 force = ClampedAdditionVector(new Vector2(spid.x, spid.z), new Vector2(vc.x, vc.z));
                Vector3 sum1 = spid + vc;
                Vector3 sum2 = spid + new Vector3(force.x, 0, force.y);
                if (sum1.magnitude > sum2.magnitude && spid.magnitude > airSpeed)
                {
                    vc.x = force.x;
                    vc.z = force.y;
                }
                rb.AddForce(vc, ForceMode.VelocityChange);
            }
        }
        if (OnGround())
        {
            EnterWalking();
        }
    }

    private void Walk(Vector3 wishDir, float maxSpeed, float Acceleration)
    {
        canDJump = true;
        if (jump)
        {
            Jump();
            EnterFlying();
        }
        else
        {
            if (running)
            {
                maxSpeed *= 1.5f;
            }
            wishDir = wishDir.normalized;
            spid = new Vector3(rb.velocity.x, 0, rb.velocity.z);
            spid = wishDir * maxSpeed - spid;
            if (spid.magnitude < maxSpeed)
            {
                Acceleration *= spid.magnitude / maxSpeed;
            }
            else
            {
                if (new Vector3(rb.velocity.x, 0, rb.velocity.z).magnitude > airTargetSpeed * 0.9f && bhopLenTimer >= 0)
                {
                    Acceleration = 0;
                }
                else
                {
                    Acceleration *= 0.5f * maxSpeed / spid.magnitude;
                }
            }
            spid = spid.normalized * Acceleration;
            float magn = spid.magnitude;
            spid = Vector3.ProjectOnPlane(spid, groundNormal);
            spid = spid.normalized;
            spid *= magn;
            rb.AddForce(spid);
        }
    }

    private void Crouch(Vector3 wishDir, float maxSpeed, float Acceleration)
    {
        if (jump)
        {
            Jump();
            EnterFlying();
        }
        else
        {
            //No sliding for now

            //spid = new Vector3(rb.velocity.x, 0, rb.velocity.z);
            maxSpeed *= 0.5f;
            //if (spid.magnitude < maxSpeed + 30)
            //{
                Acceleration *= 0.5f;
                Walk(wishDir, maxSpeed, Acceleration);
            //}
            //else
            //{
            //    spid = wishDir * maxSpeed - spid;
            //    Acceleration = spid.magnitude / maxSpeed;
            //    spid = spid.normalized * Acceleration;
            //    rb.AddForce(spid);
            //}
            //if (!OnGround())
            //    EnterFlying();
        }
    }

    private void Jump()
    {
        if (OnGround())
        {
            rb.AddForce(0, jumpForce, 0, ForceMode.Impulse);
            EnterFlying();
        }
    }

    private void DoubleJump(Vector3 wishDir)
    {
        if (canDJump)
        {
            float tempJumpForce = jumpForce;
            //Calculate upwards
            float upSpeed = rb.velocity.y;
            if (upSpeed < 0){
                upSpeed = 0;
            }
            else if(upSpeed < dJumpBaseSpd)
            {
                upSpeed = dJumpBaseSpd;
                tempJumpForce = 0;
            }

            //Calculate sideways
            Vector3 jumpVector = Vector3.zero;
            Vector2 force = Vector2.zero;
            wishDir = wishDir.normalized;
            Vector3 spid = new Vector3(rb.velocity.x, 0, rb.velocity.z);
            if (spid.magnitude > airTargetSpeed)
            {
                jumpVector = wishDir * dashForce;
                spid = spid.normalized;
                if(Vector3.Dot(spid, wishDir) > 0)
                {
                    spid = Quaternion.Euler(0,90,0) * spid;
                    jumpVector = Vector3.Project(jumpVector, spid);
                    force = ClampedAdditionVector(new Vector2(rb.velocity.x, rb.velocity.z), new Vector2(jumpVector.x, jumpVector.z));
                    jumpVector.x = force.x;
                    jumpVector.z = force.y;
                }
            }
            else
            {
                if(wishDir.magnitude != 0)
                    jumpVector = wishDir * airTargetSpeed - spid;
            }
            //Apply Jump
            jumpVector.y = tempJumpForce;
            rb.velocity = new Vector3(rb.velocity.x, upSpeed, rb.velocity.z);
            rb.AddForce(jumpVector, ForceMode.Impulse);
            canDJump = false;
        }
    }

    #endregion



    #region MathGenious

    private Vector2 ClampedAdditionVector(Vector2 a, Vector2 b)
    {
        float k, x, y;
        k = Mathf.Sqrt(Mathf.Pow(a.x, 2) + Mathf.Pow(a.y, 2)) / Mathf.Sqrt(Mathf.Pow(a.x + b.x, 2) + Mathf.Pow(a.y + b.y, 2));
        x = k * (a.x + b.x) - a.x;
        y = k * (a.y + b.y) - a.y;
        return new Vector2(x, y);
    }

    private Vector3 RotateToPlane(Vector3 vect, Vector3 normal)
    {
        Vector3 rotDir = Vector3.ProjectOnPlane(normal, Vector3.up);
        Quaternion rotation = Quaternion.AngleAxis(-90f, Vector3.up);
        rotDir = rotation * rotDir;
        float angle = -Vector3.Angle(Vector3.up, normal);
        rotation = Quaternion.AngleAxis(angle, rotDir);
        vect = rotation * vect;
        return vect;
    }

    private float WallrunCameraAngle()
    {
        Vector3 rotDir = Vector3.ProjectOnPlane(groundNormal, Vector3.up);
        Quaternion rotation = Quaternion.AngleAxis(-90f, Vector3.up);
        rotDir = rotation * rotDir;
        float angle = Vector3.SignedAngle(Vector3.up, groundNormal, Quaternion.AngleAxis(90f, rotDir) * groundNormal);
        angle -= 90;
        angle /= 180;

        Vector3 playerDir = transform.forward;
        Vector3 normal = new Vector3(groundNormal.x, 0, groundNormal.z);

        return Vector3.Cross(playerDir, normal).y * angle;
    }

    private float DistanceToWall(GameObject otherObject)
    {
        if (otherObject != null)
        {
            Vector3 closestSurfacePoint;
            Vector3 position = transform.position;
            position.y += 0.25f - height / 2f;

            closestSurfacePoint = otherObject.GetComponent<Collider>().ClosestPointOnBounds(position);
            closestSurfacePoint -= position;
            float surfaceDistance = closestSurfacePoint.magnitude;
            return surfaceDistance;
        }
        else
        {
            return -1;
        }
    }
    #endregion




    #region Setters

    public void AdjustGoundSpeed(float newValue)
    {
        groundSpeed = newValue;
    }
    public void AdjustGoundAcceleration(float newValue)
    {
        grAccel = newValue;
    }
    public void AdjustAirSpeed(float newValue)
    {
        airSpeed = newValue;
    }
    public void AdjustAirAccel(float newValue)
    {
        airAccel = newValue;
    }
    public void AdjustJumpForce(float newValue)
    {
        jumpForce = newValue;
    }
    public void AdjustBhopLeniency(float newValue)
    {
        bhopLeniency = newValue;
    }
    public void AdjustAirTargetSpeed(float newValue)
    {
        airTargetSpeed = newValue;
    }
    public void AdjustDashForce(float newValue)
    {
        dashForce = newValue;
    }
    public void AdjustDoubleJumpBaseSpeed(float newValue)
    {
        dJumpBaseSpd = newValue;
    }

    #endregion
}
