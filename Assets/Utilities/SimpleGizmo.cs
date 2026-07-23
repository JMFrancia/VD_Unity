using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;

/*
 * Simple utility to draw a standard gizmo; great for debugging
 */
public class SimpleGizmo : MonoBehaviour
{
    [SerializeField] bool _show = true;
    [SerializeField] Color _color = Color.green;
    [SerializeField] float _size = 1f;
    [SerializeField] GizmoShape _shape = GizmoShape.WireSphere;


    [Serializable]
    enum GizmoShape
    {
        Sphere,
        WireSphere,
        Cube,
        WireCube,
        Ray
    }

    private void OnDrawGizmos()
    {
        if (!_show)
            return;
        Gizmos.color = _color;
        switch (_shape) {
            case GizmoShape.WireCube:
                Gizmos.DrawWireCube(transform.position, Vector3.one * _size);
                break;
            case GizmoShape.Cube:
                Gizmos.DrawCube(transform.position, Vector3.one * _size);
                break;
            case GizmoShape.WireSphere:
                Gizmos.DrawWireSphere(transform.position, _size);
                break;
            case GizmoShape.Sphere:
                Gizmos.DrawSphere(transform.position, _size);
                break;
            case GizmoShape.Ray:
                Gizmos.DrawRay(transform.position, Vector3.up * _size);
                Gizmos.DrawRay(transform.position, Vector3.down * _size);
                break;
        }
    }
}
