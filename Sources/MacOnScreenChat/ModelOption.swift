import Foundation

enum ModelProvider: String {
    case ollama
    case gemini
}

/// A selectable model the user can switch between (local Ollama or Gemini).
struct ModelOption: Identifiable, Hashable {
    let id: String          // stable id, persisted to UserDefaults
    let label: String       // shown in the picker
    let provider: ModelProvider
    let modelName: String   // backend model identifier
}

extension ModelOption {
    /// Local models are the ones currently pulled in Ollama. Gemini models hit
    /// the hosted API (requires GEMINI_API_KEY in .env — see README).
    static let all: [ModelOption] = [
        ModelOption(id: "ollama:qwen3:30b", label: "Local · qwen3:30b", provider: .ollama, modelName: "qwen3:30b"),
        ModelOption(id: "ollama:deepseek-r1:32b", label: "Local · deepseek-r1:32b", provider: .ollama, modelName: "deepseek-r1:32b"),
        ModelOption(id: "ollama:qwen2.5-coder:7b", label: "Local · qwen2.5-coder:7b", provider: .ollama, modelName: "qwen2.5-coder:7b"),
        ModelOption(id: "gemini:gemini-2.5-flash", label: "Gemini · 2.5 Flash", provider: .gemini, modelName: "gemini-2.5-flash"),
        ModelOption(id: "gemini:gemini-2.5-pro", label: "Gemini · 2.5 Pro", provider: .gemini, modelName: "gemini-2.5-pro"),
    ]

    static let defaultOption = all[0]

    static func option(id: String?) -> ModelOption {
        all.first { $0.id == id } ?? defaultOption
    }
}
