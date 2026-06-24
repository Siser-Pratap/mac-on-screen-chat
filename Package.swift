// swift-tools-version: 6.0
import PackageDescription

let package = Package(
    name: "MacOnScreenChat",
    platforms: [
        .macOS(.v13)
    ],
    dependencies: [
        .package(url: "https://github.com/groue/GRDB.swift.git", from: "6.0.0")
    ],
    targets: [
        .executableTarget(
            name: "MacOnScreenChat",
            dependencies: [
                .product(name: "GRDB", package: "GRDB.swift")
            ],
            path: "Sources/MacOnScreenChat"
        )
    ]
)
