using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MissileScript : MonoBehaviour
{
    public float speed = 25.0f;
    public GameObject explosionPrefab;

    // Start is called before the first frame update
    //
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        this.transform.Translate(Vector3.forward * speed * Time.deltaTime);
    }

    void OnCollisionEnter(Collision collision)
    {
        // Vérifier si le missile touche un avion
        if (collision.gameObject.CompareTag("Plane") || collision.gameObject.name.Contains("Plane") || collision.gameObject.name.Contains("plane"))
        {
            // Créer l'effet d'explosion à la position du missile
            if (explosionPrefab != null)
            {
                Instantiate(explosionPrefab, transform.position, Quaternion.identity);
            }

            // Détruire le missile
            Destroy(gameObject);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        // Alternative si vous utilisez un Trigger au lieu d'un Collider
        if (other.CompareTag("Plane") || other.name.Contains("Plane") || other.name.Contains("plane"))
        {
            // Créer l'effet d'explosion à la position du missile
            if (explosionPrefab != null)
            {
                Instantiate(explosionPrefab, transform.position, Quaternion.identity);
            }

            // Détruire le missile
            Destroy(gameObject);
        }
    }
}
