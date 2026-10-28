import SwiftUI
import UIKit

/// Writes `widgets-light.png` and `widgets-dark.png` (plus `done.txt`) into Documents/boards; scripts/render-gallery.sh copies them out.
@MainActor
enum BoardRenderer {
    static let scale: CGFloat = 2

    static func renderAll() async -> String {
        let directory = outputDirectory()
        do {
            try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
        } catch {
            return "Не вдалося створити теку: \(error.localizedDescription)"
        }
        var written: [String] = []
        for scheme in [ColorScheme.light, .dark] {
            let name = scheme == .light ? "widgets-light" : "widgets-dark"
            let renderer = ImageRenderer(content: RenderBoard(scheme: scheme))
            renderer.scale = scale
            renderer.proposedSize = ProposedViewSize(width: RenderBoard.width, height: nil)
            if let image = renderer.uiImage, let data = image.pngData() {
                do {
                    try data.write(to: directory.appendingPathComponent("\(name).png"), options: .atomic)
                    written.append(name)
                } catch {
                    return "Не вдалося записати \(name): \(error.localizedDescription)"
                }
            }
        }
        try? "ok".write(to: directory.appendingPathComponent("done.txt"), atomically: true, encoding: .utf8)
        return "Записано \(written.joined(separator: ", ")) у \(directory.path)"
    }

    static func outputDirectory() -> URL {
        FileManager.default.urls(for: .documentDirectory, in: .userDomainMask)[0].appendingPathComponent("boards", isDirectory: true)
    }
}
