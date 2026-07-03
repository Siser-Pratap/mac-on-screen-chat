import Foundation

/// Optional calibration for the "Dating reply" skill. It overrides the model's
/// own read of how interested she is — which is what drives how far the three
/// reply options escalate. `auto` is the default and injects nothing (the model
/// judges from the messages, as tuned in Phase 2).
enum DatingHeat: String, CaseIterable, Identifiable {
    case auto
    case cool
    case warm
    case spicy

    var id: String { rawValue }

    /// The skill this calibration applies to.
    static let skillID = "dating"

    var label: String {
        switch self {
        case .auto:  return "Auto"
        case .cool:  return "Cool"
        case .warm:  return "Warm"
        case .spicy: return "Spicy"
        }
    }

    /// Appended to the skill's system prompt at send time. `auto` adds nothing so
    /// the model keeps reading her interest straight from the thread.
    var promptDirective: String {
        switch self {
        case .auto:
            return ""
        case .cool:
            return "\n\nCALIBRATION: Read her as low interest / cooling off, whatever the thread suggests. Keep all three options light and low-pressure; make the Spicy slot a genuine re-engagement or pattern-break, never a bold move."
        case .warm:
            return "\n\nCALIBRATION: Read her as warm but not fully committed. Moderate escalation — Flirty is fine; keep Spicy suggestive but restrained."
        case .spicy:
            return "\n\nCALIBRATION: She's clearly into it. Escalate confidently — the Spicy option can be genuinely bold and suggestive (still innuendo, never crude or explicit)."
        }
    }
}
