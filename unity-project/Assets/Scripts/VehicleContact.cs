using UnityEngine;
using System;

// One place for the rest of the game to read vehicle-to-world contacts.
// TruckController and TrailerRig both raise this; the shift log (GameFlow)
// subscribes once and turns it into warnings, contact counts and a debrief line.
public struct VehicleContact
{
    public string name;      // collider that was hit
    public Vector3 point;    // approximate world point of contact
    public float speed;      // impact speed, m/s (0 for a stationary swing)
    public bool trailer;     // true when the trailer body made contact
}

// Static relay so a contact can reach the HUD/debrief without either vehicle
// holding a reference to GameFlow. GameFlow subscribes in Start and unsubscribes
// in OnDestroy, so a scene restart does not leave a dead listener behind.
public static class VehicleContactBus
{
    public static event Action<VehicleContact> Contact;

    public static void Raise(VehicleContact contact)
    {
        var handler = Contact;
        if (handler != null) handler(contact);
    }
}
