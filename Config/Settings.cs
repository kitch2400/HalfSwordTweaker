namespace HalfSwordTweaker.Config;

/// <summary>
/// Represents the performance impact of a setting.
/// </summary>
public enum PerformanceImpact
{
    /// <summary>
    /// Minimal visual changes, significant performance gain.
    /// Suitable for low-end hardware.
    /// </summary>
    Low,

    /// <summary>
    /// Moderate visual changes, moderate performance impact.
    /// Suitable for mid-range hardware.
    /// </summary>
    Medium,

    /// <summary>
    /// Noticeable visual improvements, higher performance cost.
    /// Suitable for high-end hardware.
    /// </summary>
    High,

    /// <summary>
    /// Maximum visual quality, highest performance cost.
    /// Suitable for top-tier hardware.
    /// </summary>
    Epic
}
