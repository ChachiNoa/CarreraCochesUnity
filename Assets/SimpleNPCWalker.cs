using UnityEngine;

/// <summary>
/// Un script muy sencillo y para principiantes que hace que un NPC mueva
/// sus brazos y piernas como si estuviera caminando usando matemáticas básicas (seno).
/// Además:
/// - Se desplaza físicamente por el mapa a ras de suelo y de forma ligera para no frenar el coche.
/// - Detecta colisiones físicas con el coche, desactivando restricciones para salir volando con físicas cómicas.
/// - Cuenta con una animación mejorada que flexiona rodillas, balancea caderas y evita clipado de brazos.
/// </summary>
public class SimpleNPCWalker : MonoBehaviour
{
    [Header("Ajustes de Movimiento de Extremidades")]
    [Tooltip("Velocidad a la que se mueven los brazos y piernas.")]
    public float velocidadCaminar = 8.0f;

    [Tooltip("Ángulo máximo que se balancearán las piernas.")]
    public float anguloPiernas = 28.0f;

    [Tooltip("Ángulo máximo que se balancearán los brazos.")]
    public float anguloBrazos = 30.0f;

    [Header("Ajustes de Desplazamiento")]
    [Tooltip("Si está activo, el NPC se moverá hacia adelante.")]
    public bool seDesplaza = false;

    [Tooltip("Velocidad de avance del NPC en el mapa.")]
    public float velocidadAvance = 1.5f;

    [Header("Ajustes de Físicas y Choque")]
    [Tooltip("Fuerza con la que el NPC saldrá despedido al ser atropellado.")]
    public float fuerzaImpacto = 22.0f;

    [Tooltip("Tiempo en segundos que tardará el NPC en desaparecer tras ser golpeado.")]
    public float tiempoDesaparecer = 10.0f;

    [Tooltip("Masa del NPC (muy baja, ej: 3kg, para que salgan despedidos como muñecos sin frenar al coche).")]
    public float masaNPC = 3.0f;

    [Header("Ajustes de Sonido")]
    [Tooltip("Arrastra aquí el archivo de sonido (.mp3, .wav) que sonará al ser atropellado.")]
    public AudioClip sonidoAtropello;
    [Tooltip("Arrastra aquí el archivo de sonido (.mp3, .wav) que sonará cuando el NPC se ASUSTE con la bocina.")]
    public AudioClip sonidoSusto;

    [Header("Ajustes de Susto (Bocina)")]
    [Tooltip("Fuerza hacia ARRIBA con la que el NPC saldrá disparado al asustarse. Súbela para que vuelen más alto.")]
    public float fuerzaSustoArriba = 20f;
    [Tooltip("Fuerza de rotación aleatoria al asustarse (giros en el aire).")]
    public float fuerzaRotacionSusto = 20f;

    // Referencias a los huesos del modelo 3D
    private Transform piernaIzquierda;
    private Transform piernaDerecha;
    private Transform pantorrillaIzquierda;
    private Transform pantorrillaDerecha;
    private Transform brazoIzquierdo;
    private Transform brazoDerecho;
    private Transform hips;

    // Rotaciones y posiciones iniciales
    private Quaternion rotacionInicialPiernaIzq;
    private Quaternion rotacionInicialPiernaDer;
    private Quaternion rotacionInicialPantorrillaIzq;
    private Quaternion rotacionInicialPantorrillaDer;
    private Quaternion rotacionInicialBrazoIzq;
    private Quaternion rotacionInicialBrazoDer;
    private Quaternion rotacionInicialHips;
    private Vector3 posicionInicialHips;

    // Componentes físicos
    private Rigidbody rb;
    private CapsuleCollider capCollider;
    private bool haSidoGolpeado = false;
    private bool haSidoAsustado = false;

    // Destino al que debe caminar
    private Vector3 destinoAcera;
    private bool tieneDestino = false;

    public static bool EsNPCCompleto(GameObject obj)
    {
        string nombre = obj.name.ToLower();
        return nombre.Contains("character");
    }

    void Start()
    {
        // --- 1. CONFIGURACIÓN FÍSICA NO KINEMATIC (Blanda en colisiones) ---
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }
        rb.mass = masaNPC;
        rb.isKinematic = false; // No es kinematic, por lo que reacciona de inmediato al coche
        rb.useGravity = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        
        // Bloqueamos las rotaciones para que el personaje camine erguido y no se caiga solo
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY | RigidbodyConstraints.FreezeRotationZ;

        capCollider = GetComponent<CapsuleCollider>();
        if (capCollider == null)
        {
            capCollider = gameObject.AddComponent<CapsuleCollider>();
            capCollider.center = new Vector3(0, 0.9f, 0);
            capCollider.radius = 0.3f;
            capCollider.height = 1.8f;
        }

        // --- 2. DETECCIÓN Y GUARDADO DE HUESOS ---
        piernaIzquierda = BuscarHueso(transform, "LeftUpLeg");
        piernaDerecha = BuscarHueso(transform, "RightUpLeg");
        pantorrillaIzquierda = BuscarHueso(transform, "LeftLeg");
        pantorrillaDerecha = BuscarHueso(transform, "RightLeg");
        brazoIzquierdo = BuscarHueso(transform, "LeftArm");
        brazoDerecho = BuscarHueso(transform, "RightArm");
        hips = BuscarHueso(transform, "Hips");
        if (hips == null) hips = BuscarHueso(transform, "Pelvis");

        // Guardar posiciones y rotaciones iniciales
        if (piernaIzquierda != null) rotacionInicialPiernaIzq = piernaIzquierda.localRotation;
        if (piernaDerecha != null) rotacionInicialPiernaDer = piernaDerecha.localRotation;
        if (pantorrillaIzquierda != null) rotacionInicialPantorrillaIzq = pantorrillaIzquierda.localRotation;
        if (pantorrillaDerecha != null) rotacionInicialPantorrillaDer = pantorrillaDerecha.localRotation;
        if (brazoIzquierdo != null) rotacionInicialBrazoIzq = brazoIzquierdo.localRotation;
        if (brazoDerecho != null) rotacionInicialBrazoDer = brazoDerecho.localRotation;
        if (hips != null)
        {
            rotacionInicialHips = hips.localRotation;
            posicionInicialHips = hips.localPosition;
        }
    }

    public void SetTarget(Vector3 target)
    {
        destinoAcera = target;
        tieneDestino = true;
        seDesplaza = true;

        Vector3 direccion = (destinoAcera - transform.position);
        direccion.y = 0f;
        if (direccion.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(direccion);
        }
    }

    void Update()
    {
        if (haSidoGolpeado) return;

        // --- SEGUIMIENTO DEL OBJETIVO ---
        if (tieneDestino)
        {
            Vector3 direccion = (destinoAcera - transform.position);
            direccion.y = 0f;
            if (direccion.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direccion), Time.deltaTime * 5.0f);
            }

            float distancia = Vector3.Distance(
                new Vector3(transform.position.x, 0, transform.position.z),
                new Vector3(destinoAcera.x, 0, destinoAcera.z)
            );

            if (distancia < 0.8f)
            {
                if (NPCGameManager.Instance != null)
                {
                    NPCGameManager.Instance.RegistrarPeatonCruzado();
                }
                Destroy(gameObject);
                return;
            }
        }

        // --- ANIMACIÓN DE EXTREMIDADES MEJORADA ---
        float ondaSeno = Mathf.Sin(Time.time * velocidadCaminar);
        float anguloActualLeg = ondaSeno * anguloPiernas;
        float anguloActualArm = ondaSeno * anguloBrazos;

        // 1. Balanceo de muslos
        if (piernaIzquierda != null)
            piernaIzquierda.localRotation = rotacionInicialPiernaIzq * Quaternion.Euler(anguloActualLeg, 0, 0);
        if (piernaDerecha != null)
            piernaDerecha.localRotation = rotacionInicialPiernaDer * Quaternion.Euler(-anguloActualLeg, 0, 0);

        // 2. Flexión de rodillas (Pantorrillas): flexionan cuando la pierna va hacia adelante
        if (pantorrillaIzquierda != null)
        {
            float anguloKneeIzq = (ondaSeno < 0) ? -ondaSeno * 40f : 0f;
            pantorrillaIzquierda.localRotation = rotacionInicialPantorrillaIzq * Quaternion.Euler(anguloKneeIzq, 0, 0);
        }
        if (pantorrillaDerecha != null)
        {
            float anguloKneeDer = (ondaSeno > 0) ? ondaSeno * 40f : 0f;
            pantorrillaDerecha.localRotation = rotacionInicialPantorrillaDer * Quaternion.Euler(anguloKneeDer, 0, 0);
        }

        // 3. Balanceo de brazos con inclinación hacia afuera para evitar atravesar el torso
        if (brazoIzquierdo != null)
            brazoIzquierdo.localRotation = rotacionInicialBrazoIzq * Quaternion.Euler(-anguloActualArm, anguloActualArm * 0.2f, -12f);
        if (brazoDerecho != null)
            brazoDerecho.localRotation = rotacionInicialBrazoDer * Quaternion.Euler(anguloActualArm, -anguloActualArm * 0.2f, 12f);

        // 4. Balanceo y oscilación de cadera (Hips)
        if (hips != null)
        {
            // Rotación lateral sutil en Y
            hips.localRotation = rotacionInicialHips * Quaternion.Euler(0, ondaSeno * 4f, 0);
            
            // Oscilación vertical (botar hacia arriba y abajo con cada paso: frecuencia doble)
            float oscilacionY = Mathf.Abs(Mathf.Sin(Time.time * velocidadCaminar * 2f)) * 0.04f;
            hips.localPosition = posicionInicialHips + new Vector3(0, oscilacionY - 0.02f, 0);
        }

        // --- DESPLAZAMIENTO FÍSICO ---
        if (seDesplaza)
        {
            transform.Translate(Vector3.forward * velocidadAvance * Time.deltaTime);
        }
    }

    // --- DETECCIÓN DE COLISIÓN (El atropello) ---
    private void OnCollisionEnter(Collision collision)
    {
        if (haSidoGolpeado) return;

        bool esCoche = collision.gameObject.GetComponent<CarControl>() != null || 
                       collision.gameObject.CompareTag("Player") ||
                       collision.gameObject.name.ToLower().Contains("car") ||
                       collision.gameObject.name.ToLower().Contains("coche");

        if (esCoche)
        {
            DesatarFisicasDeCaida(collision);
        }
    }

    private void DesatarFisicasDeCaida(Collision collision)
    {
        haSidoGolpeado = true;
        tieneDestino = false;
        seDesplaza = false;

        if (NPCGameManager.Instance != null)
        {
            NPCGameManager.Instance.RegistrarPeatonAtropellado();
        }

        // Reproducir el sonido en el punto de colisión en el espacio 3D
        if (sonidoAtropello != null)
        {
            AudioSource.PlayClipAtPoint(sonidoAtropello, transform.position);
        }

        // Liberamos todas las restricciones físicas para que ruede, gire y vuele con total libertad
        rb.constraints = RigidbodyConstraints.None;
        rb.isKinematic = false;

        // Calculamos la dirección de empuje
        Vector3 direccionEmpuje = (transform.position - collision.transform.position).normalized;
        direccionEmpuje.y = 0.45f; // Un empuje hacia arriba sutil pero genial para hacerlo volar
        direccionEmpuje = direccionEmpuje.normalized;

        // Añadimos fuerza impulsiva y torque para que de vueltas graciosas en el aire
        rb.AddForce(direccionEmpuje * fuerzaImpacto, ForceMode.Impulse);

        Vector3 rotacionGraciosa = new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), Random.Range(-1f, 1f));
        rb.AddTorque(rotacionGraciosa * fuerzaImpacto, ForceMode.Impulse);

        Destroy(gameObject, tiempoDesaparecer);
    }

    /// <summary>
    /// Hace que el peatón se asuste (por ejemplo, con el claxon), saliendo despedido bruscamente
    /// hacia arriba y dando vueltas en el aire.
    /// </summary>
    public void Asustar()
    {
        if (haSidoGolpeado || haSidoAsustado) return;

        haSidoAsustado = true;
        haSidoGolpeado = true;
        tieneDestino = false;
        seDesplaza = false;

        if (NPCGameManager.Instance != null)
        {
            NPCGameManager.Instance.RegistrarPeatonAsustado();
        }

        // Reproducir el sonido de susto en el punto 3D del NPC
        if (sonidoSusto != null)
        {
            AudioSource.PlayClipAtPoint(sonidoSusto, transform.position);
        }

        // Liberamos restricciones físicas para que salga volando y dando vueltas
        rb.constraints = RigidbodyConstraints.None;
        rb.isKinematic = false;

        // Fuerza hacia arriba configurable desde el Inspector
        rb.AddForce(Vector3.up * fuerzaSustoArriba, ForceMode.Impulse);

        // Torque aleatorio para rotación brusca configurable
        Vector3 rotacionSusto = new Vector3(Random.Range(-1.5f, 1.5f), Random.Range(-1.5f, 1.5f), Random.Range(-1.5f, 1.5f));
        rb.AddTorque(rotacionSusto * fuerzaRotacionSusto, ForceMode.Impulse);

        Destroy(gameObject, tiempoDesaparecer);
    }

    private Transform BuscarHueso(Transform parent, string nombreBuscado)
    {
        if (parent.name == nombreBuscado)
            return parent;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform encontrado = BuscarHueso(parent.GetChild(i), nombreBuscado);
            if (encontrado != null)
                return encontrado;
        }

        return null;
    }
}
