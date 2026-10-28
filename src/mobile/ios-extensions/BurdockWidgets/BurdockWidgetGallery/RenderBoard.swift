import SwiftUI
import WidgetKit
import BurdockShared

/// One appearance of the render board: every scenario × every family at iPhone 6.1" widget sizes.
struct RenderBoard: View {
    static let width: CGFloat = 1000

    let scheme: ColorScheme

    private let scenarios: [(title: String, model: WordWidgetModel)] = [
        ("Цвіт · типове слово", .preview()),
        ("Довге слово", .preview(entry: PreviewData.longEntry())),
        ("Переклад схований («спочатку вгадай»)", .preview(hidden: true)),
        ("Безкоштовний режим · пул дня вичерпано", .preview(progress: DayProgress(shown: 5, planned: 5), freeTier: true)),
        ("Тема «Луг»", .preview(themeId: "luh")),
        ("Тема «Ніч»", .preview(themeId: "nich")),
        ("Тема «Сонце»", .preview(themeId: "sontse")),
        ("Шкала вичерпана · Реп’яшок спить", .fallback()),
    ]

    var body: some View {
        VStack(alignment: .leading, spacing: 28) {
            Text(verbatim: scheme == .light ? "Віджети «Реп’ях» · світла тема" : "Віджети «Реп’ях» · темна тема")
                .font(.title2.weight(.bold))
                .foregroundStyle(Color(BurdockTokens.Colors.textPrimary))
            ForEach(Array(scenarios.enumerated()), id: \.offset) { item in
                ScenarioRow(title: item.element.title, model: item.element.model)
            }
        }
        .padding(24)
        .frame(width: Self.width, alignment: .leading)
        .background(Color(BurdockTokens.Colors.surfaceDefault))
        .environment(\.colorScheme, scheme)
    }
}

struct ScenarioRow: View {
    let title: String
    let model: WordWidgetModel

    var body: some View {
        VStack(alignment: .leading, spacing: 12) {
            Text(verbatim: title)
                .font(.headline)
                .foregroundStyle(Color(BurdockTokens.Colors.textSecondary))
            HStack(alignment: .top, spacing: 16) {
                WidgetFrame(model: model, family: .systemSmall, size: CGSize(width: 170, height: 170))
                VStack(alignment: .leading, spacing: 16) {
                    WidgetFrame(model: model, family: .systemMedium, size: CGSize(width: 364, height: 170))
                    HStack(alignment: .top, spacing: 12) {
                        AccessoryFrame(model: model, family: .accessoryRectangular, size: CGSize(width: 172, height: 76))
                        AccessoryFrame(model: model, family: .accessoryCircular, size: CGSize(width: 76, height: 76))
                        AccessoryFrame(model: model, family: .accessoryInline, size: CGSize(width: 240, height: 30))
                    }
                }
                WidgetFrame(model: model, family: .systemLarge, size: CGSize(width: 364, height: 382))
            }
        }
    }
}

/// A home screen widget as the system hosts it: 16 pt content margins on a rounded gradient card.
struct WidgetFrame: View {
    let model: WordWidgetModel
    let family: WidgetFamily
    let size: CGSize

    var body: some View {
        WordWidgetView(model: model, family: family)
            .padding(WidgetMetrics.s4)
            .frame(width: size.width, height: size.height)
            .background(model.palette.gradient)
            .clipShape(RoundedRectangle(cornerRadius: 22, style: .continuous))
    }
}

/// A lock screen accessory as the system hosts it: translucent dark plate, white text.
struct AccessoryFrame: View {
    let model: WordWidgetModel
    let family: WidgetFamily
    let size: CGSize

    var body: some View {
        WordWidgetView(model: model, family: family)
            .padding(family == .accessoryInline ? 4 : 8)
            .frame(width: size.width, height: size.height)
            .foregroundStyle(.white)
            .background(Color.black.opacity(0.6))
            .clipShape(RoundedRectangle(cornerRadius: family == .accessoryCircular ? size.width / 2 : 14, style: .continuous))
            .environment(\.colorScheme, .dark)
    }
}
