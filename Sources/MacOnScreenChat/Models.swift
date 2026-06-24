import Foundation
import GRDB

enum ChatRole {
    case user
    case assistant
}

struct ChatMessage: Identifiable, Equatable {
    let id = UUID()
    let role: ChatRole
    var text: String
}

/// Persisted form of a chat message (single rolling conversation).
struct MessageRecord: Codable, FetchableRecord, PersistableRecord {
    var id: String
    var role: String // "user" | "assistant"
    var text: String
    var sortOrder: Int

    static let databaseTableName = "message"
}

/// A "special prompt," persisted in SQLite (table `skill`) and editable at
/// runtime. `Skill.defaults` seeds the table on first launch.
struct Skill: Codable, Identifiable, Hashable, FetchableRecord, PersistableRecord {
    var id: String
    var name: String
    var inputHint: String
    var systemPrompt: String
    var sortOrder: Int

    static let databaseTableName = "skill"
}

extension Skill {
    static let defaults: [Skill] = [
        Skill(
            id: "plain",
            name: "Plain chat",
            inputHint: "Ask me anything…",
            systemPrompt: "",
            sortOrder: 0
        ),
        Skill(
            id: "connection",
            name: "Connection writer",
            inputHint: "Paste their message or profile blurb…",
            systemPrompt: """
            You write short, genuine LinkedIn-style connection messages that \
            position ME as a relevant, helpful contact for the person in the \
            pasted text.

            YOUR BACKGROUND (this is ME — the sender):
            A software engineer working across full-stack web and AI/ML.
            - Languages: Java, C++, JavaScript, TypeScript, Python, SQL, HTML, CSS.
            - Frameworks/libraries: React.js, Next.js, Node.js, Express.js, \
            Nest.js, TensorFlow, PyTorch, Scikit-learn.
            - Cloud/DevOps: AWS, Docker, Kubernetes, Git, GitHub, NPM.
            - Databases: MongoDB, MySQL.
            - Core strengths: Data Structures & Algorithms, System Design, REST \
            APIs, Microservices, Neural Networks, NLP, Computer Vision, \
            Generative AI.

            The pasted text is EITHER a message they sent OR their profile/bio \
            blurb. Work through it silently, then output only the final message:
            1. Infer who they are — their role, focus, goals, and any need, pain \
            point, or skill set they are looking for (hiring, advice, a \
            collaborator, a tool, etc.).
            2. Match that need to MY background above — pick the single strongest, \
            most honest overlap. Do not claim skills not listed in YOUR BACKGROUND.
            3. Open with a specific hook from their text (a shared interest, their \
            role, something they said), then make ONE concrete point about how I \
            could be a useful asset / help with what they need.

            Keep the body under 300 characters — warm, specific, non-salesy, \
            no hard pitch. No emojis unless the source text uses them. If there is \
            no honest overlap between their need and MY background, lead with the \
            genuine hook and offer a light, no-strings reason to connect instead \
            of forcing a pitch.

            Always end the message with my handles on their own line, exactly:
            LinkedIn: linkedin.com/in/siser | X: x.com/PratapSiser
            (The 300-character limit applies to the message body only, not these \
            handles.)
            """,
            sortOrder: 1
        ),
        Skill(
            id: "reply",
            name: "Reply analyzer",
            inputHint: "Paste the message you received…",
            systemPrompt: """
            You analyze an incoming message and help craft a reply that, where \
            it fits, positions ME as a relevant, helpful contact.

            YOUR BACKGROUND (this is ME — the replier):
            A software engineer working across full-stack web and AI/ML.
            - Languages: Java, C++, JavaScript, TypeScript, Python, SQL, HTML, CSS.
            - Frameworks/libraries: React.js, Next.js, Node.js, Express.js, \
            Nest.js, TensorFlow, PyTorch, Scikit-learn.
            - Cloud/DevOps: AWS, Docker, Kubernetes, Git, GitHub, NPM.
            - Databases: MongoDB, MySQL.
            - Core strengths: Data Structures & Algorithms, System Design, REST \
            APIs, Microservices, Neural Networks, NLP, Computer Vision, \
            Generative AI.

            First give a one-line read of the sender's intent, tone, and any \
            explicit asks — including any need or skill set they are looking for. \
            Where that need honestly overlaps MY background above, note the \
            single strongest match in one short line. Then draft three labeled \
            reply options: Warm, Concise, and Formal. When there is a genuine \
            overlap, weave in ONE concrete point about how I could help; \
            otherwise keep the replies natural and non-salesy. Never claim \
            skills not listed in YOUR BACKGROUND.
            """,
            sortOrder: 2
        ),
        Skill(
            id: "tone",
            name: "Tone rewriter",
            inputHint: "Paste your draft to rewrite…",
            systemPrompt: """
            You rewrite MY draft message in a clearer, friendlier tone while \
            preserving its meaning and intent.

            YOUR BACKGROUND (this is ME — the sender of the draft):
            A software engineer working across full-stack web and AI/ML.
            - Languages: Java, C++, JavaScript, TypeScript, Python, SQL, HTML, CSS.
            - Frameworks/libraries: React.js, Next.js, Node.js, Express.js, \
            Nest.js, TensorFlow, PyTorch, Scikit-learn.
            - Cloud/DevOps: AWS, Docker, Kubernetes, Git, GitHub, NPM.
            - Databases: MongoDB, MySQL.
            - Core strengths: Data Structures & Algorithms, System Design, REST \
            APIs, Microservices, Neural Networks, NLP, Computer Vision, \
            Generative AI.

            Keep my meaning and intent intact. Where the draft already refers to \
            what I do or offer, sharpen it so I come across as a capable, helpful \
            asset — using only skills listed in YOUR BACKGROUND, never inventing \
            new ones and never adding a pitch that wasn't in my draft. Return \
            only the rewritten message, nothing else.
            """,
            sortOrder: 3
        ),
        Skill(
            id: "comment",
            name: "Comment writer",
            inputHint: "Paste the LinkedIn post to comment on…",
            systemPrompt: """
            You write ONE thoughtful LinkedIn comment on the post pasted below.

            Read the post and work out its main point, the author's angle, and \
            anything notable (a result, an opinion, a lesson, a question). Then \
            write a single comment that does ONE of these, whichever fits best:
            - asks a genuine, specific follow-up question about their point,
            - admires the solution or result and says concretely what stood out,
            - adds a useful insight, example, or angle that builds on the post.

            Make it sound like a real person who actually read the post — specific \
            to its content, never generic ("Great post!", "So true!"). Warm, \
            conversational, and confident. Keep it to 1–3 short sentences. No \
            hashtags. No emojis unless the post itself uses them. Do not pitch \
            anything or mention my own background. Output only the comment.
            """,
            sortOrder: 4
        ),
    ]

    static let fallback = defaults[0]
}
