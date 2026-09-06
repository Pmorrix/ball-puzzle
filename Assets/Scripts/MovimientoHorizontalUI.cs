using UnityEngine;

public class MovimientoHorizontalUI : MonoBehaviour
{
    [SerializeField] private float velocidad = 1f;
    [SerializeField] private float distancia = 20f;

    private RectTransform rectTransform;
    private Vector2 posicionInicial;
    private float tiempo;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        posicionInicial = rectTransform.anchoredPosition;
        ReiniciarMovimiento();
    }

    private void OnEnable()
    {
        // Cada activación vuelve a empezar desde la posición original.
        if (rectTransform == null)
        {
            rectTransform = GetComponent<RectTransform>();
            posicionInicial = rectTransform.anchoredPosition;
        }

        ReiniciarMovimiento();
    }

    private void ReiniciarMovimiento()
    {
        tiempo = 0f;
        rectTransform.anchoredPosition = posicionInicial;
    }

    private void Update()
    {
        // El ciclo empieza en la posición original y respeta la pausa.
        tiempo += Time.deltaTime * velocidad;

        // El valor absoluto evita que la mano pase al lado derecho.
        float desplazamiento = Mathf.Abs(Mathf.Sin(tiempo)) * distancia;

        rectTransform.anchoredPosition =
            posicionInicial + Vector2.left * desplazamiento;
    }
}
