using System;

namespace Fatiro.Domain
{
    /// <summary>Strategy: one way of loading the structure. New cases need no solver changes.</summary>
    public interface ILoadCase
    {
        LoadCaseKind Kind { get; }
        float Magnitude { get; }
        string Describe();
        void Apply(LoadContext context);
    }

    /// <summary>Shared behaviour for load cases with a single magnitude setting.</summary>
    public abstract class LoadCase : ILoadCase
    {
        protected LoadCase(float magnitude, float minimum, float maximum)
        {
            if (magnitude < minimum || magnitude > maximum)
                throw new ArgumentOutOfRangeException(nameof(magnitude), $"{GetType().Name} must be between {minimum} and {maximum}.");
            Magnitude = magnitude;
        }

        public abstract LoadCaseKind Kind { get; }
        public float Magnitude { get; }
        public abstract string Unit { get; }

        public virtual string Describe() => $"{EnumText.LoadCaseName(Kind)} {Magnitude:0.#} {Unit}".Trim();

        public abstract void Apply(LoadContext context);
    }

    /// <summary>Self weight plus finishes. Always applied before any other case.</summary>
    public sealed class DeadLoad : LoadCase
    {
        public const float RoofFinish = 1.0f;

        /// <summary>A ferrocement or glass shell needs only a waterproof coat, not battens and a ceiling.</summary>
        public const float ShellFinish = 0.3f;
        public const float FloorFinish = 1.5f;

        public DeadLoad() : base(1f, 1f, 1f) { }

        public override LoadCaseKind Kind => LoadCaseKind.Dead;
        public override string Unit => string.Empty;
        public override string Describe() => "Dead load";

        public static float WeightOf(StructuralMember member)
        {
            var weight = member.SelfWeight;
            if (member.Kind == MemberKind.Panel && member.Spec.IsRoof)
            {
                var shell = member.Material.Id == MaterialLibrary.Ferrocement || member.Material.Id == MaterialLibrary.CharcoalFerrocement ||
                            member.Material.Id == MaterialLibrary.Glass;
                weight += (shell ? ShellFinish : RoofFinish) * member.Geometry.Area;
            }
            weight += FloorFinish * member.Spec.TributaryArea;
            return weight;
        }

        public override void Apply(LoadContext context)
        {
            foreach (var member in context.ActiveMembers)
                context.AddVertical(member, WeightOf(member));
        }
    }

    /// <summary>People and furniture, in kN/m² of floor.</summary>
    public sealed class LiveLoad : LoadCase
    {
        public LiveLoad(float kiloNewtonsPerSquareMetre) : base(kiloNewtonsPerSquareMetre, 0f, 5f) { }

        public override LoadCaseKind Kind => LoadCaseKind.Live;
        public override string Unit => "kN/m²";

        public override void Apply(LoadContext context)
        {
            foreach (var member in context.ActiveMembers)
                if (member.Spec.TributaryArea > 0f)
                    context.AddVertical(member, Magnitude * member.Spec.TributaryArea);
        }
    }

    /// <summary>Wind speed in km/h. Pushes walls sideways and lifts roofs.</summary>
    public sealed class WindLoad : LoadCase
    {
        public WindLoad(float kilometresPerHour) : base(kilometresPerHour, 0f, 150f) { }

        public override LoadCaseKind Kind => LoadCaseKind.Wind;
        public override string Unit => "km/h";

        /// <summary>Dynamic pressure q = ½ρv², in kN/m².</summary>
        public float Pressure
        {
            get
            {
                var metresPerSecond = Magnitude / 3.6f;
                return 0.613f * metresPerSecond * metresPerSecond / 1000f;
            }
        }

        public override void Apply(LoadContext context)
        {
            var q = Pressure;
            foreach (var member in context.ActiveMembers)
            {
                if (member.Kind != MemberKind.Panel) continue;
                var area = member.Geometry.Area;
                context.AddLateral(q * Calibration.WindPressureCoefficient * area * 0.5f);
                if (member.Spec.IsRoof) context.AddUplift(member, q * Calibration.WindUpliftCoefficient * area);
            }
        }
    }

    /// <summary>
    /// Rainfall in mm/h. Working drains carry roof runoff away; without them water ponds on the
    /// roof and adds weight. 1 mm/h on 1 m² is 1 litre per hour.
    /// </summary>
    public sealed class RainLoad : LoadCase
    {
        public RainLoad(float millimetresPerHour) : base(millimetresPerHour, 0f, 100f) { }

        public override LoadCaseKind Kind => LoadCaseKind.Rain;
        public override string Unit => "mm/h";

        public override void Apply(LoadContext context)
        {
            var roofArea = 0f;
            var drains = 0;
            foreach (var member in context.ActiveMembers)
            {
                if (member.Kind == MemberKind.Panel && member.Spec.IsRoof) roofArea += member.Geometry.Area;
                if (member.Kind == MemberKind.Drain) drains++;
            }

            var runoff = Magnitude * roofArea;
            context.RunoffLitresPerHour = runoff;

            var ponding = context.DesignHasDrainage && drains == 0;
            var loadPerSquareMetre = Magnitude * (ponding ? Calibration.PondingLoadPerMm : Calibration.DrainedRainLoadPerMm);

            foreach (var member in context.ActiveMembers)
            {
                if (member.Kind == MemberKind.Panel && member.Spec.IsRoof)
                    context.AddVertical(member, loadPerSquareMetre * member.Geometry.Area);
                if (member.Kind == MemberKind.Drain)
                    context.AddDrainFlow(member, runoff / drains);
            }

            context.DrainedLitresPerHour = drains > 0 || !context.DesignHasDrainage ? runoff : 0f;
        }
    }

    /// <summary>Ground shaking level 0–5. Sideways force grows with weight, soft soil and slope.</summary>
    public sealed class SeismicLoad : LoadCase
    {
        public SeismicLoad(float level) : base(level, 0f, 5f) { }

        public override LoadCaseKind Kind => LoadCaseKind.Seismic;
        public override string Unit => "level";
        public override string Describe() => $"Earthquake level {Magnitude:0}";

        public float Coefficient(SiteConditions site) =>
            Calibration.SeismicCoefficientPerLevel * Magnitude * site.SeismicAmplification * (1f + site.SlopeDegrees / 60f);

        public override void Apply(LoadContext context)
        {
            context.AddLateral(Coefficient(context.Site) * context.ShakingWeight());
        }
    }

    /// <summary>Settings for a load test as chosen on the sliders (FR-15).</summary>
    public sealed class LoadCaseConfig
    {
        public LoadCaseConfig(LoadCaseKind kind, float magnitude)
        {
            Kind = kind;
            Magnitude = magnitude;
        }

        public LoadCaseKind Kind { get; }
        public float Magnitude { get; }

        public static float Minimum(LoadCaseKind kind) => 0f;

        public static float Maximum(LoadCaseKind kind)
        {
            switch (kind)
            {
                case LoadCaseKind.Dead: return 1f;
                case LoadCaseKind.Live: return 5f;
                case LoadCaseKind.Wind: return 150f;
                case LoadCaseKind.Rain: return 100f;
                default: return 5f;
            }
        }

        public static float Default(LoadCaseKind kind)
        {
            switch (kind)
            {
                case LoadCaseKind.Dead: return 1f;
                case LoadCaseKind.Live: return 3f;
                case LoadCaseKind.Wind: return 120f;
                case LoadCaseKind.Rain: return 80f;
                default: return 3f;
            }
        }

        public static bool IsWholeNumber(LoadCaseKind kind) => kind == LoadCaseKind.Seismic;
    }

    /// <summary>Builds the right load case strategy for a configuration.</summary>
    public static class LoadCaseFactory
    {
        public static ILoadCase Create(LoadCaseConfig config)
        {
            switch (config.Kind)
            {
                case LoadCaseKind.Dead: return new DeadLoad();
                case LoadCaseKind.Live: return new LiveLoad(config.Magnitude);
                case LoadCaseKind.Wind: return new WindLoad(config.Magnitude);
                case LoadCaseKind.Rain: return new RainLoad(config.Magnitude);
                case LoadCaseKind.Seismic: return new SeismicLoad(config.Magnitude);
                default: throw new ArgumentOutOfRangeException(nameof(config), config.Kind, "Unknown load case.");
            }
        }
    }
}
