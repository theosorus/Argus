using UnityEngine;

public class PlaneController : MonoBehaviour
{
    [Header("Plane stats")]
    public float throttleIncrement = 50f;   // Amount to increase/decrease throttle per frame
    public float maxThrust = 190f;           // NOTE: avec throttle 0..100 => thrust max = maxThrust * 100
    public float responsiveness = 0.015f;       // How quickly the plane responds to input


    [Header("Supported Cams")]
    public Camera[] cams;

    [Header("Aerodynamics (simple model)")]
    [Tooltip("Air density (kg/m^3)")]
    public float airDensity = 1.225f;

    [Tooltip("Wing reference area (m^2)")]
    public float wingArea = 16f;

    [Tooltip("Lift slope per radian (~ 3 à 6). 5.5 est doux.")]
    public float ClAlpha = 5.5f;

    [Tooltip("Lift at 0° AoA (camber).")]
    public float Cl0 = 0.2f;

    [Tooltip("Max lift coefficient (stall soft)")]
    public float ClMax = 1.4f;

    [Tooltip("Profile drag at zero-lift")]
    public float Cd0 = 0.03f;

    [Tooltip("Induced drag factor k")]
    public float inducedDrag = 0.04f;

    [Header("Gravity")]
    [Tooltip("Si vrai: on laisse Rigidbody.useGravity (Unity). Si faux: on applique la gravité à la main.")]
    public bool useBuiltInGravity = false;

    private float throttle; // 0..100 (inchangé)
    private float pitch;
    private float roll;
    private float yaw;

    private Rigidbody rb;

    private float responseModifier
    {
        get { return (rb.mass / 10f) * responsiveness; }
    }

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogError("[BiPlaneController] Rigidbody manquant.");
        }

        // Cohérent avec le toggle de gravité choisi
        if (rb != null) rb.useGravity = useBuiltInGravity;
    }

    private void HandleInput()
    {

        
        roll  = Input.GetAxis("Horizontal"); // A/D ou ← →
        pitch = Input.GetAxis("Vertical");   // W/S ou ↑ ↓
        // Ajout d'une zone morte pour limiter les mouvements trop sensibles
        if(Mathf.Abs(roll) < 0.5f) roll = 0f;
        if(Mathf.Abs(pitch) < 0.5f) pitch = 0f;
        yaw   = 0f;                          // Contrôle de lacet désactivé

        if (Input.GetKey(KeyCode.Space))       throttle += throttleIncrement;
        else if (Input.GetKey(KeyCode.LeftControl)) throttle -= throttleIncrement;

        throttle = Mathf.Clamp(throttle, 0f, 100f); // throttle pourcent
    }

    void Update()
    {
        HandleInput();
    }

    void FixedUpdate()
    {
        if (rb == null) return;

        // === THRUST (poussée) — inchangé pour ne rien casser ===
        // Thrust max réel = maxThrust * 100 quand throttle = 100.
        rb.AddForce(transform.forward * maxThrust * throttle, ForceMode.Force);

        // --- Aérodynamique : LIFT & DRAG ---
        Vector3 v = rb.velocity;
        float speed = v.magnitude;

        if (speed > 0.1f)
        {
            // Vent relatif (depuis l'avant vers l'arrière)
            Vector3 relativeWind = -v;

            // Direction du lift : perpendiculaire au vent relatif et à l'envergure
            // (envergure = transform.right). Donne une portance "vers le haut de l'aile".
            Vector3 liftDir = Vector3.Cross(relativeWind, transform.right).normalized;

            // Direction du drag : opposée à la vitesse
            Vector3 dragDir = -v.normalized;

            // Angle d'attaque (AoA) simple : composante verticale vs avant local
            Vector3 vLocal = transform.InverseTransformDirection(v);
            float aoa = Mathf.Atan2(vLocal.y, Mathf.Abs(vLocal.z) + 0.001f); // radians

            // Coeffs aéros
            float Cl = Mathf.Clamp(Cl0 + ClAlpha * aoa, -ClMax, ClMax);
            float Cd = Cd0 + inducedDrag * Cl * Cl;

            // Pression dynamique
            float q = 0.5f * airDensity * speed * speed;

            // Forces
            Vector3 lift = liftDir * (q * wingArea * Cl);
            Vector3 drag = dragDir * (q * wingArea * Cd);

            rb.AddForce(lift, ForceMode.Force);
            rb.AddForce(drag, ForceMode.Force);
        }

        // --- GRAVITY ---
        // Si tu veux gérer la gravité toi‑même, décoche "Use Gravity" du Rigidbody
        // et mets useBuiltInGravity = false.
        if (!useBuiltInGravity)
        {
            rb.AddForce(Physics.gravity * rb.mass, ForceMode.Force);
        }

        // === Contrôles (inchangés) ===
        rb.AddTorque(transform.right   * pitch * responseModifier, ForceMode.Force);
        rb.AddTorque(transform.up      * yaw   * responseModifier, ForceMode.Force);
        rb.AddTorque(-transform.forward * roll * responseModifier, ForceMode.Force);
    }
}
