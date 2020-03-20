using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    //Ground
    public float groundSpeed = 3.44f;
    public float grAccel = 200f;

    //Air
    public float airSpeed = 3f;
    public float airAccel = 0.3f;
    public float airTargetSpeed = 15f;

    //Wall
    public float wallAccel = 5f;
    public float wallStickiness = 10f;
    public float wallStickDistance = 0.5f;
    public float wallRunTilt = 15f;
    public float wallFloorBarrier = 40f;
    public float wallBanTime = 0.3f;
    public float wallrunTime = 4f;

    //Jump
    public float jumpForce = 6f;
    public float dashForce = 4f;
    public float bhopLeniency = 0.1f;
    public float dJumpBaseSpd = 8;

    bool jump;
    bool canDJump = false;

    bool running = false;
    bool crouching = false;
    bool grounded = false;

    Vector3 groundNormal;
    Vector3 bannedGroundNormal;

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

    //Timers
    private Timer bhopLenTimer;
    private Timer wallrunTimer;
    private Timer wallDetachTimer;
    private Timer wallBanTimer;
    private Timer jumpTimer;

    //GameObjects
    public Collider wallRunCollider;
    public CapsuleCollider heightCollider;
    public Camera cam;
    private Rigidbody rb;
    private CapsuleCollider pm;
    private PlayerCameraController camCon;

    void Start()
    {
        //Timers
        bhopLenTimer = gameObject.AddComponent<Timer>();
        bhopLenTimer.SetTime(bhopLeniency);

        wallBanTimer = gameObject.AddComponent<Timer>();
        wallBanTimer.SetTime(wallBanTime);

        wallDetachTimer = gameObject.AddComponent<Timer>();
        wallDetachTimer.SetTime(0.2f);

        wallrunTimer = gameObject.AddComponent<Timer>();
        wallrunTimer.SetTime(wallrunTime);

        jumpTimer = gameObject.AddComponent<Timer>();
        jumpTimer.SetTime(0.05f);


        //GameObjects
        camCon = cam.GetComponent<PlayerCameraController>();
        height = transform.GetChild(0).GetComponent<Renderer>().bounds.size.y;
        rb = GetComponent<Rigidbody>();
        pm = GetComponent<CapsuleCollider>();
        camCon.SetTilt(0);

        //Setting up beggining state
        EnterFlying();
        canDJump = true;
        pm.material.dynamicFriction = 0;
        pm.material.frictionCombine = PhysicMaterialCombine.Minimum;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetAxisRaw("Mouse ScrollWheel") != 0)
            jump = true;
        if (Input.GetKey(KeyCode.LeftShift) && Input.GetAxisRaw("Vertical") == 1)
            running = true;
        crouching = (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.C));
        if (Input.GetKeyDown(KeyCode.V))
        {
            rb.AddForce(dir.normalized * 20f, ForceMode.VelocityChange);
        }
        dir = Direction();
    }

    private void FixedUpdate()
    {
        //if (crouching)
        //{
        //    //THIS IS BROKEN AND OFTEN CAUSES UNCONTROLLED ROTATAION
        //    //Unity really doesnt like crouching and freeze rotation gets thrown out of the window when you crouch
        //    //Ive got no idea rn how to fix that because its an engine bug and shoudnt happen (ther are no warning in docs that changing colliders may cause this)
        //    //I noticed that when you already start spinning looking to the right will help you for some reason

        //    cam.transform.localPosition = new Vector3(0f, Mathf.Lerp(cam.transform.localPosition.y, 1.3f, 0.5f), 0f);
        //    heightCollider.center = new Vector3(0f, Mathf.Lerp(heightCollider.center.y, 0.8f, 0.3f), 0f);
        //}
        //else
        //{
        //    cam.transform.localPosition = new Vector3(0f, Mathf.Lerp(cam.transform.localPosition.y, 1.8f, 0.5f), 0f);
        //    heightCollider.center = new Vector3(0f, Mathf.Lerp(heightCollider.center.y, 1.25f, 0.3f), 0f);
        //}

        if (wallDetachTimer.Done() && !wallBanTimer.Done())
        {
            bannedGroundNormal = groundNormal;
        }
        else
        {
            bannedGroundNormal = Vector3.zero;
        }

        if (state == State.Walking && rb.velocity.magnitude < 0.5f)
        {
            pm.material.dynamicFriction = 1f;
            pm.material.frictionCombine = PhysicMaterialCombine.Maximum;
        }
        else
        {
            pm.material.dynamicFriction = 0f;
            pm.material.frictionCombine = PhysicMaterialCombine.Minimum;
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

        jump = false;
        running = false;
    }

    void OnCollisionStay(Collision collision)
    {
        if (collision.contactCount > 0)
        {
            float angle;
            foreach (ContactPoint contact in collision.contacts)
            {
                angle = Vector3.Angle(contact.normal, Vector3.up);
                if(angle < wallFloorBarrier)
                {
                    EnterWalking();
                    grounded = true;
                    groundNormal = contact.normal;
                    return;
                }
            }
            if (VectorToGround().magnitude > 0.2f)
            {
                grounded = false;
            }
            if (grounded == false)
            {
                foreach (ContactPoint contact in collision.contacts)
                {
                    if (contact.thisCollider == wallRunCollider && state != State.Walking)
                    {
                        angle = Vector3.Angle(contact.normal, Vector3.up);
                        if (angle > wallFloorBarrier && angle < 120f)
                        {
                            grounded = true;
                            groundNormal = contact.normal;
                            EnterWallrun();
                            return;
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



    #region EnterState
    private void EnterWalking()
    {
        if (state != State.Walking)
        {
            SoundManagerScript.PlaySound("land");
            bhopLenTimer.StartTimer();
            state = State.Walking;
        }
    }

    private void EnterFlying( bool wishFly = false)
    {
        grounded = false;
        if(state == State.Wallruning && VectorToWall().magnitude < wallStickDistance && !wishFly)
        {
            return;
        }
        else if (state != State.Flying)
        {
            wallBanTimer.StartTimer();
            canDJump = true;
            state = State.Flying;
        }
    }

    private void EnterWallrun()
    {
        if (state != State.Wallruning && VectorToGround().magnitude > 0.2f && CanRunOnThisWall(bannedGroundNormal) && wallDetachTimer.Done())
        {
            wallrunTimer.StartTimer();
            canDJump = true;
            state = State.Wallruning;
        }
    }

    #endregion



    #region Movement

    private void Wallrun(Vector3 wishDir, float maxSpeed, float Acceleration)
    {
        if (jump)
        {
            Vector3 direction = new Vector3(groundNormal.x, 1, groundNormal.z);
            rb.AddForce(direction * jumpForce, ForceMode.Impulse);
            EnterFlying(true);
        }
        else if (wallrunTimer.Done() || crouching)
        {
            rb.AddForce(groundNormal * 3f, ForceMode.Impulse);
            EnterFlying(true);
        }
        else
        {
            //Variables
            Vector3 distance = VectorToWall();
            wishDir = wishDir.normalized;
            wishDir = RotateToPlane(wishDir, -distance.normalized);
            wishDir *= wallAccel;
            Vector3 antiGravityForce = -Physics.gravity;
            Vector3 verticalDamping;
            

            //Calculating forces
            if ( Mathf.Sign(rb.velocity.y) != Mathf.Sign(wishDir.y) && rb.velocity.y < 0)
            {
                verticalDamping = new Vector3(0, -rb.velocity.y * 0.1f, 0);
                rb.AddForce(verticalDamping, ForceMode.VelocityChange);
            }
            else
            {
                verticalDamping = new Vector3(0, -rb.velocity.y * 0.5f, 0);
                rb.AddForce(verticalDamping, ForceMode.Acceleration);
            }
            if(wishDir.y < -0.3)
            {
                wishDir.y *= 2f;
            }
            Vector3 horizontalDamping = new Vector3(-rb.velocity.x, 0, -rb.velocity.z);
            if (Vector3.Dot(horizontalDamping, new Vector3(wishDir.x, 0, wishDir.z)) > 0f)
            {
                horizontalDamping *= (horizontalDamping - wishDir).magnitude;
            }
            else
            {
                horizontalDamping = Vector3.ClampMagnitude(horizontalDamping, 3);
            }

            if (wallrunTimer.FractionLeft() < 0.33)
            {
                antiGravityForce *= wallrunTimer.FractionLeft();
            }
            if (distance.magnitude > wallStickDistance) distance = Vector3.zero;


            //Adding forces
            rb.AddForce(antiGravityForce);
            rb.AddForce(distance.normalized * wallStickiness * Mathf.Clamp(distance.magnitude / wallStickDistance, 0, 1), ForceMode.Acceleration);
            rb.AddForce(horizontalDamping, ForceMode.Acceleration);
            rb.AddForce(wishDir);
        }
        if (!grounded)
        {
            wallDetachTimer.StartTimer();
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
                Vector2 force = ClampedAdditionVector(new Vector2(spid.x, spid.z) * 2f, new Vector2(vc.x, vc.z) * 2f);
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
        if (grounded)
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
                if (new Vector3(rb.velocity.x, 0, rb.velocity.z).magnitude > airTargetSpeed * 0.9f && !bhopLenTimer.Done())
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
            //if (!grounded())
            //    EnterFlying();
        }
    }

    private void Jump()
    {
        if (state == State.Walking && jumpTimer.Done())
        {
            SoundManagerScript.PlaySound("jump");
            rb.AddForce(0, jumpForce, 0, ForceMode.Impulse);
            EnterFlying();
            jumpTimer.StartTimer();
        }
    }

    private void DoubleJump(Vector3 wishDir)
    {
        if (canDJump)
        {
            SoundManagerScript.PlaySound("djump");
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
            if(spid.magnitude < airTargetSpeed)
            {
                jumpVector = wishDir * dashForce;
                jumpVector -= spid;
            }
            else if (Vector3.Dot(spid.normalized, wishDir) > -0.4f)
            {
                jumpVector = wishDir * dashForce;
                force = ClampedAdditionVector(new Vector2(rb.velocity.x, rb.velocity.z), new Vector2(jumpVector.x, jumpVector.z));
                jumpVector.x = force.x;
                jumpVector.z = force.y;
            }
            else
            {
                jumpVector = wishDir * spid.magnitude;
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

    private bool CanRunOnThisWall(Vector3 normal)
    {
        if(Vector3.Angle(normal, groundNormal) > 10 || wallBanTimer.Done())
        {
            return true;
        }
        else
        {
            return false;
        }
    }

    private Vector3 VectorToWall()
    {
        Vector3 direction;
        Vector3 position = transform.position + Vector3.up * 0.1f;
        RaycastHit hit;
        if (Physics.Raycast(position, -groundNormal, out hit, wallStickDistance) && Vector3.Angle(groundNormal, hit.normal) < 70)
        {
            groundNormal = hit.normal;
            Physics.Raycast(position, -groundNormal, out hit, wallStickDistance);
            direction = hit.point - position;
            return direction;
        }
        else
        {
            return Vector3.positiveInfinity;
        }
    }

    private Vector3 VectorToGround()
    {
        Vector3 position = transform.position;
        RaycastHit hit;
        if (Physics.Raycast(position, Vector3.down, out hit, wallStickDistance))
        {
            return hit.point - position;
        }
        else
        {
            return Vector3.positiveInfinity;
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
