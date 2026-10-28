import SwiftUI

/// Debug-only iOS app that renders the widget views outside WidgetKit (spec 04 §3.1.1, §3.8): CI launches it on a simulator,
/// it writes the light and dark boards to its Documents folder, and the run uploads them as artifacts. Never published.
@main
struct BurdockWidgetGalleryApp: App {
    var body: some Scene {
        WindowGroup {
            GalleryRootView()
        }
    }
}

struct GalleryRootView: View {
    @State private var status = "Рендер…"

    var body: some View {
        ScrollView([.vertical, .horizontal]) {
            VStack(alignment: .leading, spacing: 32) {
                Text(status)
                    .font(.footnote)
                    .textSelection(.enabled)
                RenderBoard(scheme: .light)
                RenderBoard(scheme: .dark)
            }
            .padding()
        }
        .task {
            status = await BoardRenderer.renderAll()
        }
    }
}
