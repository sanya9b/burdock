// swift-tools-version: 5.9
// Shared models and rules of the widget extension (spec 04 §3.1.1, §3.2.4): Foundation only, so the tests run with
// `swift test` on the macOS runner without a simulator.
import PackageDescription

let package = Package(
    name: "BurdockShared",
    platforms: [.iOS(.v16), .macOS(.v13)],
    products: [
        .library(name: "BurdockShared", targets: ["BurdockShared"])
    ],
    targets: [
        .target(name: "BurdockShared"),
        .testTarget(name: "BurdockSharedTests", dependencies: ["BurdockShared"])
    ]
)
