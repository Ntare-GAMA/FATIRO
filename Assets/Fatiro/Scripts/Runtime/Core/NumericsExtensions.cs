using UnityEngine;
using NVector3 = System.Numerics.Vector3;

namespace Fatiro.Core
{
    /// <summary>Converts between the domain's System.Numerics vectors and Unity vectors.</summary>
    public static class NumericsExtensions
    {
        public static Vector3 ToUnity(this NVector3 value) => new Vector3(value.X, value.Y, value.Z);
        public static NVector3 ToNumerics(this Vector3 value) => new NVector3(value.x, value.y, value.z);
    }
}
