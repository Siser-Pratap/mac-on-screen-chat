// swift-tools-version: 6.0
import PackageDescription

let package = Package(
    name: "MacOnScreenChat",
    platforms: [
        .macOS(.v13)
    ],
    targets: [
        .executableTarget(
            name: "MacOnScreenChat",
            path: "Sources/MacOnScreenChat"
        )
    ]
)
