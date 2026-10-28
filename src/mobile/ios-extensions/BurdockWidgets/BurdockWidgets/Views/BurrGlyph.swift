import SwiftUI

/// Procedural burr, the mascot's silhouette (spec 02 §3.12): shapes only, so tinted and vibrant modes keep it and no raster lives in the extension.
struct BurrGlyph: View {
    enum Eyes {
        case none
        case open
        case asleep
    }

    var size: CGFloat
    var color: Color
    var eyeColor: Color = .clear
    var eyes: Eyes = .none

    var body: some View {
        ZStack {
            ForEach(0..<12, id: \.self) { index in
                Capsule()
                    .fill(color)
                    .frame(width: size * 0.09, height: size * 0.24)
                    .offset(y: -size * 0.40)
                    .rotationEffect(.degrees(Double(index) * 30 + 15))
            }
            Circle()
                .fill(color)
                .frame(width: size * 0.64, height: size * 0.64)
            face
        }
        .frame(width: size, height: size)
        .accessibilityHidden(true)
    }

    @ViewBuilder
    private var face: some View {
        switch eyes {
        case .none:
            EmptyView()
        case .open:
            HStack(spacing: size * 0.12) {
                Circle().fill(eyeColor).frame(width: size * 0.10, height: size * 0.10)
                Circle().fill(eyeColor).frame(width: size * 0.10, height: size * 0.10)
            }
            .offset(y: -size * 0.04)
        case .asleep:
            HStack(spacing: size * 0.10) {
                Capsule().fill(eyeColor).frame(width: size * 0.14, height: size * 0.035)
                Capsule().fill(eyeColor).frame(width: size * 0.14, height: size * 0.035)
            }
            .offset(y: -size * 0.02)
        }
    }
}
