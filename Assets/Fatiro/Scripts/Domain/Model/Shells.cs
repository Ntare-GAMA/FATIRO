using System;
using System.Collections.Generic;
using System.Numerics;

namespace Fatiro.Domain
{
    /// <summary>
    /// One cross-section of a lofted shell, the way an architect models a curved building: a
    /// rounded arch of the given half-width rising from its base to its top at distance Z along
    /// the building. Roundness 1 gives a straight-sided gable, 2 an ellipse, 4+ a boxy car body.
    /// </summary>
    public readonly struct ShellStation
    {
        public ShellStation(float z, float baseY, float topY, float halfWidth, float roundness = 2.5f, float centreX = 0f,
            bool baseOnGround = false)
        {
            Z = z;
            BaseY = baseY;
            TopY = topY;
            HalfWidth = halfWidth;
            Roundness = Math.Max(0.5f, roundness);
            CentreX = centreX;
            BaseOnGround = baseOnGround;
        }

        public float Z { get; }

        /// <summary>Height of the shell's lower edge; measured from the ground under it when BaseOnGround.</summary>
        public float BaseY { get; }

        /// <summary>Absolute height of the crown.</summary>
        public float TopY { get; }

        public float HalfWidth { get; }
        public float Roundness { get; }
        public float CentreX { get; }
        public bool BaseOnGround { get; }

        public ShellStation WithBase(float baseY) => new ShellStation(Z, baseY, TopY, HalfWidth, Roundness, CentreX, false);
    }

    /// <summary>A lofted surface through ordered stations: a creature's body, a car's shell, a roof or an ear canopy.</summary>
    public sealed class ShellProfile
    {
        public ShellProfile(string name, IReadOnlyList<ShellStation> stations)
        {
            if (stations == null || stations.Count < 2) throw new ArgumentException($"Shell {name} needs at least two stations.");
            Name = name;
            Stations = stations;
        }

        public string Name { get; }
        public IReadOnlyList<ShellStation> Stations { get; }

        /// <summary>Stations with every base turned into an absolute height for this site.</summary>
        public ShellStation[] Resolve(SiteLayout layout)
        {
            var resolved = new ShellStation[Stations.Count];
            for (var i = 0; i < Stations.Count; i++)
            {
                var station = Stations[i];
                var ground = station.BaseOnGround && layout != null ? layout.GroundY(station.Z) : 0f;
                resolved[i] = station.WithBase(ground + station.BaseY);
            }
            return resolved;
        }
    }

    /// <summary>
    /// The piece of a shell one member covers: between two stations, and between two positions
    /// round the section (0 = right-hand base, 0.5 = crown, 1 = left-hand base). A patch whose
    /// stations are equal is an end cap that closes the section at that station.
    /// </summary>
    public readonly struct ShellPatch
    {
        public ShellPatch(int profile, int from, int to, float aroundFrom, float aroundTo)
        {
            Profile = profile;
            From = from;
            To = to;
            AroundFrom = aroundFrom;
            AroundTo = aroundTo;
            IsSet = true;
        }

        public int Profile { get; }
        public int From { get; }
        public int To { get; }
        public float AroundFrom { get; }
        public float AroundTo { get; }
        public bool IsSet { get; }
        public bool IsCap => From == To;
    }

    /// <summary>Engine-free geometry of shells, shared by the authoring tools, the solver and the mesh builder.</summary>
    public static class ShellMath
    {
        /// <summary>Point on a station's section at position t (0 right base, 0.5 crown, 1 left base).</summary>
        public static Vector3 Point(in ShellStation station, float t)
        {
            var angle = Math.PI * t;
            var c = Math.Cos(angle);
            var s = Math.Sin(angle);
            var e = 2.0 / station.Roundness;
            var x = station.CentreX + station.HalfWidth * Math.Sign(c) * Math.Pow(Math.Abs(c), e);
            var y = station.BaseY + (station.TopY - station.BaseY) * Math.Pow(Math.Abs(s), e);
            return new Vector3((float)x, (float)y, station.Z);
        }

        /// <summary>Surface area of a patch (or a cap) and its centre, sampled finely.</summary>
        public static (float area, Vector3 centre) Measure(IReadOnlyList<ShellStation> stations, ShellPatch patch)
        {
            const int steps = 16;
            var area = 0f;
            var weighted = Vector3.Zero;
            if (patch.IsCap)
            {
                var station = stations[patch.From];
                var basePoint = new Vector3(station.CentreX, station.BaseY, station.Z);
                for (var i = 0; i < steps; i++)
                {
                    var p0 = Point(station, Lerp(patch.AroundFrom, patch.AroundTo, (float)i / steps));
                    var p1 = Point(station, Lerp(patch.AroundFrom, patch.AroundTo, (float)(i + 1) / steps));
                    var triangle = Vector3.Cross(p0 - basePoint, p1 - basePoint).Length() * 0.5f;
                    area += triangle;
                    weighted += (basePoint + p0 + p1) / 3f * triangle;
                }
            }
            else
            {
                // A patch runs through every station between From and To, so it follows the shell's curve.
                for (var k = Math.Min(patch.From, patch.To); k < Math.Max(patch.From, patch.To); k++)
                {
                    var a = stations[k];
                    var b = stations[k + 1];
                    for (var i = 0; i < steps; i++)
                    {
                        var t0 = Lerp(patch.AroundFrom, patch.AroundTo, (float)i / steps);
                        var t1 = Lerp(patch.AroundFrom, patch.AroundTo, (float)(i + 1) / steps);
                        var p00 = Point(a, t0);
                        var p01 = Point(a, t1);
                        var p10 = Point(b, t0);
                        var p11 = Point(b, t1);
                        var quad = Vector3.Cross(p01 - p00, p10 - p00).Length() * 0.5f + Vector3.Cross(p11 - p01, p10 - p01).Length() * 0.5f;
                        area += quad;
                        weighted += (p00 + p01 + p10 + p11) * 0.25f * quad;
                    }
                }
            }
            return (area, area > 0f ? weighted / area : Point(stations[patch.From], (patch.AroundFrom + patch.AroundTo) * 0.5f));
        }

        public static float Lerp(float a, float b, float t) => a + (b - a) * t;
    }
}
