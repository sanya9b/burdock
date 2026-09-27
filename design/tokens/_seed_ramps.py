import math, json, os, sys
sys.stdout.reconfigure(encoding='utf-8')
HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, '..', '..'))

def hex2rgb(h):
    h = h.lstrip('#'); return tuple(int(h[i:i+2], 16) for i in (0, 2, 4))
def rgb2hex(r, g, b): return '#%02X%02X%02X' % (r, g, b)
def lin(c):
    c = c / 255
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4
def unlin(c):
    c = max(0, min(1, c))
    return 12.92 * c if c <= 0.0031308 else 1.055 * (c ** (1 / 2.4)) - 0.055
def lum(h):
    r, g, b = hex2rgb(h); return 0.2126 * lin(r) + 0.7152 * lin(g) + 0.0722 * lin(b)
def contrast(a, b):
    la, lb = lum(a), lum(b)
    if la < lb: la, lb = lb, la
    return (la + 0.05) / (lb + 0.05)
def srgb2oklab(h):
    r, g, b = [lin(x) for x in hex2rgb(h)]
    l = 0.4122214708 * r + 0.5363325363 * g + 0.0514459929 * b
    m = 0.2119034982 * r + 0.6806995451 * g + 0.1073969566 * b
    s = 0.0883024619 * r + 0.2817188376 * g + 0.6299787005 * b
    l_, m_, s_ = l ** (1 / 3), m ** (1 / 3), s ** (1 / 3)
    return (0.2104542553 * l_ + 0.7936177850 * m_ - 0.0040720468 * s_,
            1.9779984951 * l_ - 2.4285922050 * m_ + 0.4505937099 * s_,
            0.0259040371 * l_ + 0.7827717662 * m_ - 0.8086757660 * s_)
def oklab2srgb_lin(L, a, b):
    l_ = L + 0.3963377774 * a + 0.2158037573 * b
    m_ = L - 0.1055613458 * a - 0.0638541728 * b
    s_ = L - 0.0894841775 * a - 1.2914855480 * b
    l, m, s = l_ ** 3, m_ ** 3, s_ ** 3
    return (4.0767416621 * l - 3.3077115913 * m + 0.2309699292 * s,
            -1.2684380046 * l + 2.6097574011 * m - 0.3413193965 * s,
            -0.0041960863 * l - 0.7034186147 * m + 1.7076147010 * s)
def oklch(h):
    L, a, b = srgb2oklab(h); return L, math.hypot(a, b), math.degrees(math.atan2(b, a)) % 360
def from_oklch(L, C, H):
    for i in range(300):
        a = C * math.cos(math.radians(H)); b = C * math.sin(math.radians(H))
        r, g, bb = oklab2srgb_lin(L, a, b)
        if all(-0.0005 <= x <= 1.0005 for x in (r, g, bb)):
            return rgb2hex(*[round(unlin(x) * 255) for x in (r, g, bb)])
        C *= 0.98
    return rgb2hex(*[round(unlin(max(0, min(1, x))) * 255) for x in oklab2srgb_lin(L, 0, 0)])
def tune(C, H, target_lum):
    lo, hi = 0.05, 0.98
    for _ in range(60):
        mid = (lo + hi) / 2
        if lum(from_oklch(mid, C, H)) < target_lum: lo = mid
        else: hi = mid
    return from_oklch((lo + hi) / 2, C, H)

steps = [50, 100, 200, 300, 400, 500, 600, 700, 800, 900, 950]
targetL = {50: 0.975, 100: 0.945, 200: 0.89, 300: 0.82, 400: 0.74, 500: 0.65, 600: 0.56, 700: 0.47, 800: 0.39, 900: 0.31, 950: 0.24}
def ramp(base, base_step):
    L0, C0, H0 = oklch(base); out = {}
    for s in steps:
        if s == base_step: out[s] = base.upper(); continue
        tl = targetL[s]
        if s < base_step:
            frac = (tl - targetL[base_step]) / (targetL[50] - targetL[base_step]); L = L0 + frac * (0.985 - L0)
        else:
            frac = (targetL[base_step] - tl) / (targetL[base_step] - targetL[950]); L = L0 - frac * (L0 - 0.24)
        d = abs(L - L0); span = (0.985 - L0) if L > L0 else (L0 - 0.24)
        C = C0 * (1 - 0.7 * (d / span) ** 1.4)
        out[s] = from_oklch(L, max(C, 0.0), H0)
    return out
bases = {'violet': ('#7C5CFF', 500), 'lime': ('#B4F461', 400), 'coral': ('#FF6B6B', 400), 'sunny': ('#FFC53D', 400), 'sky': ('#4CC9F0', 400)}
P = {n: ramp(h, s) for n, (h, s) in bases.items()}
# luminance-targeted tuning of the steps that carry AA-critical roles
_, Cv, Hv = oklch('#7C5CFF'); P['violet'][600] = tune(Cv, Hv, 0.140)
_, Cl, Hl = oklch('#B4F461'); P['lime'][600] = tune(Cl * 0.9, Hl, 0.270); P['lime'][700] = tune(Cl * 0.8, Hl, 0.150)
nsteps = [0, 50, 100, 200, 300, 400, 500, 600, 700, 800, 900, 950, 1000]
Lb, Cb, Hb = oklch('#F7F6FC'); Ld, Cd, Hd = oklch('#15131F'); Hn = (Hb + Hd) / 2
ntargetL = {100: 0.945, 200: 0.89, 300: 0.79, 400: 0.68, 500: 0.535, 600: 0.46, 700: 0.39, 800: 0.32, 900: 0.265, 1000: 0.155}
N = {}
for s in nsteps:
    if s == 0: N[s] = '#FFFFFF'
    elif s == 50: N[s] = '#F7F6FC'
    elif s == 950: N[s] = '#15131F'
    else:
        L = ntargetL[s]; N[s] = from_oklch(L, 0.012 + 0.008 * (1 - L), Hn)
N[400] = tune(0.015, Hn, 0.260)
N[500] = tune(0.016, Hn, 0.143)
P['neutral'] = N
def c(name):
    fam, step = name.split('.'); return P[fam][int(step)]

light = {
 'brand.primary': 'violet.600', 'brand.onPrimary': '#FFFFFF', 'brand.primaryVivid': 'violet.500',
 'brand.primaryContainer': 'violet.100', 'brand.onPrimaryContainer': 'violet.800',
 'brand.accent': 'lime.400', 'brand.onAccent': 'lime.950', 'brand.accentText': 'lime.700',
 'surface.default': 'neutral.50', 'surface.elevated': 'neutral.0', 'surface.sunken': 'neutral.100', 'surface.overlay': '#0C0B148F',
 'surface.brandTint': 'violet.50',
 'text.primary': 'neutral.950', 'text.secondary': 'neutral.600', 'text.tertiary': 'neutral.500', 'text.inverse': 'neutral.50', 'text.link': 'violet.700', 'text.disabled': 'neutral.400',
 'border.default': 'neutral.200', 'border.strong': 'neutral.400', 'border.focus': 'violet.600',
 'feedback.success': 'lime.700', 'feedback.successContainer': 'lime.100', 'feedback.onSuccessContainer': 'lime.800',
 'feedback.error': 'coral.600', 'feedback.errorContainer': 'coral.100', 'feedback.onErrorContainer': 'coral.800',
 'feedback.warning': 'sunny.700', 'feedback.warningContainer': 'sunny.100', 'feedback.onWarningContainer': 'sunny.800',
 'feedback.info': 'sky.700', 'feedback.infoContainer': 'sky.100', 'feedback.onInfoContainer': 'sky.800',
 'reward.gold': 'sunny.400', 'reward.goldText': 'sunny.800', 'reward.goldContainer': 'sunny.100',
 'reward.currency': 'lime.600', 'reward.currencyText': 'lime.800', 'reward.currencyContainer': 'lime.100',
 'reward.heart': 'coral.400',
 'streak.active': 'sunny.500', 'streak.text': 'sunny.800', 'streak.container': 'sunny.100', 'streak.frozen': 'sky.600', 'streak.frozenText': 'sky.800', 'streak.inactive': 'neutral.300',
 'quiz.correct': 'lime.700', 'quiz.correctContainer': 'lime.100', 'quiz.correctBorder': 'lime.600',
 'quiz.incorrect': 'coral.600', 'quiz.incorrectContainer': 'coral.100', 'quiz.incorrectBorder': 'coral.500',
 'quiz.selected': 'violet.700', 'quiz.selectedContainer': 'violet.100', 'quiz.selectedBorder': 'violet.600',
 'widget.background': 'violet.600', 'widget.backgroundEnd': 'violet.800', 'widget.word': '#FFFFFF', 'widget.ipa': 'violet.100', 'widget.translation': 'violet.100', 'widget.example': 'violet.100',
 'widget.accent': 'lime.400', 'widget.onAccent': 'lime.950', 'widget.chipBackground': 'violet.800', 'widget.chipText': 'violet.100', 'widget.button': '#FFFFFF', 'widget.onButton': 'violet.700', 'widget.divider': 'violet.500',
 'widget.badgeFree': 'violet.800', 'widget.onBadgeFree': 'violet.100',
}
dark = {
 'brand.primary': 'violet.400', 'brand.onPrimary': 'neutral.950', 'brand.primaryVivid': 'violet.500',
 'brand.primaryContainer': 'violet.900', 'brand.onPrimaryContainer': 'violet.200',
 'brand.accent': 'lime.400', 'brand.onAccent': 'lime.950', 'brand.accentText': 'lime.300',
 'surface.default': 'neutral.950', 'surface.elevated': 'neutral.900', 'surface.sunken': 'neutral.1000', 'surface.overlay': '#000000A3',
 'surface.brandTint': 'violet.950',
 'text.primary': 'neutral.50', 'text.secondary': 'neutral.300', 'text.tertiary': 'neutral.400', 'text.inverse': 'neutral.950', 'text.link': 'violet.300', 'text.disabled': 'neutral.600',
 'border.default': 'neutral.800', 'border.strong': 'neutral.400', 'border.focus': 'violet.300',
 'feedback.success': 'lime.300', 'feedback.successContainer': 'lime.900', 'feedback.onSuccessContainer': 'lime.200',
 'feedback.error': 'coral.300', 'feedback.errorContainer': 'coral.900', 'feedback.onErrorContainer': 'coral.200',
 'feedback.warning': 'sunny.300', 'feedback.warningContainer': 'sunny.900', 'feedback.onWarningContainer': 'sunny.200',
 'feedback.info': 'sky.300', 'feedback.infoContainer': 'sky.900', 'feedback.onInfoContainer': 'sky.200',
 'reward.gold': 'sunny.400', 'reward.goldText': 'sunny.300', 'reward.goldContainer': 'sunny.900',
 'reward.currency': 'lime.400', 'reward.currencyText': 'lime.300', 'reward.currencyContainer': 'lime.900',
 'reward.heart': 'coral.400',
 'streak.active': 'sunny.400', 'streak.text': 'sunny.300', 'streak.container': 'sunny.900', 'streak.frozen': 'sky.400', 'streak.frozenText': 'sky.300', 'streak.inactive': 'neutral.700',
 'quiz.correct': 'lime.300', 'quiz.correctContainer': 'lime.900', 'quiz.correctBorder': 'lime.500',
 'quiz.incorrect': 'coral.300', 'quiz.incorrectContainer': 'coral.900', 'quiz.incorrectBorder': 'coral.500',
 'quiz.selected': 'violet.200', 'quiz.selectedContainer': 'violet.900', 'quiz.selectedBorder': 'violet.400',
 'widget.background': 'violet.900', 'widget.backgroundEnd': 'neutral.950', 'widget.word': 'neutral.50', 'widget.ipa': 'violet.200', 'widget.translation': 'violet.200', 'widget.example': 'violet.300',
 'widget.accent': 'lime.400', 'widget.onAccent': 'lime.950', 'widget.chipBackground': 'violet.800', 'widget.chipText': 'violet.100', 'widget.button': 'violet.300', 'widget.onButton': 'violet.950', 'widget.divider': 'violet.800',
 'widget.badgeFree': 'violet.800', 'widget.onBadgeFree': 'violet.100',
}
DESC = {
 'brand.primary': 'Заливка основних інтерактивних елементів із текстом (кнопки, активні стани). Гарантує ≥4.5:1 з onPrimary.',
 'brand.onPrimary': 'Текст та іконки на brand.primary.',
 'brand.primaryVivid': 'Фірмовий фіолет #7C5CFF без змін: іконки, ілюстрації, великий дисплейний текст (≥24pt), кільце прогресу. Не для дрібного тексту.',
 'brand.primaryContainer': 'М’яка фіолетова підкладка (виділені картки, обрані чіпи).',
 'brand.onPrimaryContainer': 'Текст на primaryContainer.',
 'brand.accent': 'Лаймова заливка акцентів (бейджі, підсвітка успіху, індикатор «нове»). Завжди з темним чорнилом onAccent.',
 'brand.onAccent': 'Темне чорнило на лаймовій заливці.',
 'brand.accentText': 'Лайм як текст або іконка на світлих поверхнях (похідний lime.700).',
 'surface.default': 'Фон екрана.',
 'surface.elevated': 'Картки, аркуші, панелі над фоном.',
 'surface.sunken': 'Заглиблені зони: поля вводу, групи налаштувань, скелетони.',
 'surface.overlay': 'Затемнення під BottomSheet/Dialog (8-значний hex з альфою).',
 'surface.brandTint': 'Легкий фірмовий тон для hero-зон і онбордингу.',
 'text.primary': 'Основний текст.', 'text.secondary': 'Другорядний текст, підписи.', 'text.tertiary': 'Підказки, метадані; лише на default/elevated.',
 'text.inverse': 'Текст на інверсних поверхнях (тости).', 'text.link': 'Посилання та текстові кнопки.', 'text.disabled': 'Неактивний текст (звільнено від вимог контрасту).',
 'border.default': 'Тонкі роздільники, контури карток.', 'border.strong': 'Контури полів вводу та контролів (≥3:1 з фоном).', 'border.focus': 'Кільце фокуса (клавіатура, Switch Control).',
 'feedback.success': 'Текст/іконка успіху.', 'feedback.successContainer': 'Підкладка успіху.', 'feedback.onSuccessContainer': 'Текст на підкладці успіху.',
 'feedback.error': 'Текст/іконка помилки.', 'feedback.errorContainer': 'Підкладка помилки.', 'feedback.onErrorContainer': 'Текст на підкладці помилки.',
 'feedback.warning': 'Текст/іконка попередження.', 'feedback.warningContainer': 'Підкладка попередження.', 'feedback.onWarningContainer': 'Текст на підкладці попередження.',
 'feedback.info': 'Текст/іконка інформації.', 'feedback.infoContainer': 'Підкладка інформації.', 'feedback.onInfoContainer': 'Текст на підкладці інформації.',
 'reward.gold': 'Золота заливка нагород (медалі, конфеті).', 'reward.goldText': 'Текст нагород на світлих поверхнях і goldContainer.', 'reward.goldContainer': 'Підкладка RewardCard.',
 'reward.currency': 'Іконка «колючки».', 'reward.currencyText': 'Число колючок у CurrencyPill.', 'reward.currencyContainer': 'Фон CurrencyPill.',
 'reward.heart': 'Декоративне серце «улюблене слово» (не несе змісту без підпису).',
 'streak.active': 'Заливка вогника активної серії.', 'streak.text': 'Число днів серії.', 'streak.container': 'Фон StreakBadge.', 'streak.frozen': 'Іконка замороженої серії.', 'streak.frozenText': 'Текст замороженого стану.', 'streak.inactive': 'Неактивний день у календарі.',
 'quiz.correct': 'Текст/іконка правильної відповіді.', 'quiz.correctContainer': 'Заливка правильного варіанта.', 'quiz.correctBorder': 'Контур правильного варіанта.',
 'quiz.incorrect': 'Текст/іконка неправильної відповіді.', 'quiz.incorrectContainer': 'Заливка неправильного варіанта.', 'quiz.incorrectBorder': 'Контур неправильного варіанта.',
 'quiz.selected': 'Текст обраного варіанта до перевірки.', 'quiz.selectedContainer': 'Заливка обраного варіанта.', 'quiz.selectedBorder': 'Контур обраного варіанта.',
 'widget.background': 'Початок градієнта фону віджета (світліший край — саме з ним рахується контраст).', 'widget.backgroundEnd': 'Кінець градієнта фону віджета.',
 'widget.word': 'Англійське слово.', 'widget.ipa': 'Транскрипція.', 'widget.translation': 'Переклад.', 'widget.example': 'Приклад речення (medium/large).',
 'widget.accent': 'Акцент віджета: індикатор рівня, підкреслення.', 'widget.onAccent': 'Чорнило на акценті.',
 'widget.chipBackground': 'Чіп категорії/рівня.', 'widget.chipText': 'Текст чіпа.',
 'widget.button': 'Кнопка «Наступне» (iOS 17+).', 'widget.onButton': 'Іконка на кнопці.', 'widget.divider': 'Роздільник у systemLarge.',
 'widget.badgeFree': 'Бейдж «5/5 сьогодні» у безкоштовному режимі.', 'widget.onBadgeFree': 'Текст бейджа.',
}
def resolve(v): return v if v.startswith('#') else c(v)
SEM = {'light': {k: resolve(v) for k, v in light.items()}, 'dark': {k: resolve(v) for k, v in dark.items()}}
REF = {'light': light, 'dark': dark}

themes = {
 'tsvit': {'label': 'Цвіт', 'unlock': 0, 'desc': 'Стандартна фірмова тема: фіолетовий градієнт, лаймовий акцент.', 'light': {}, 'dark': {}},
 'luh': {'label': 'Луг', 'unlock': 120, 'desc': 'Світла лаймова галявина з фіолетовим акцентом; уночі — темна зелень.',
   'light': {'widget.background': 'lime.100', 'widget.backgroundEnd': 'lime.200', 'widget.word': 'lime.950', 'widget.ipa': 'lime.800', 'widget.translation': 'lime.800', 'widget.example': 'lime.800', 'widget.accent': 'violet.600', 'widget.onAccent': '#FFFFFF', 'widget.chipBackground': 'lime.300', 'widget.chipText': 'lime.900', 'widget.button': 'lime.950', 'widget.onButton': 'lime.100', 'widget.divider': 'lime.300', 'widget.badgeFree': 'lime.300', 'widget.onBadgeFree': 'lime.900'},
   'dark': {'widget.background': 'lime.950', 'widget.backgroundEnd': 'neutral.950', 'widget.word': 'lime.100', 'widget.ipa': 'lime.300', 'widget.translation': 'lime.300', 'widget.example': 'lime.300', 'widget.accent': 'lime.400', 'widget.onAccent': 'lime.950', 'widget.chipBackground': 'lime.900', 'widget.chipText': 'lime.200', 'widget.button': 'lime.400', 'widget.onButton': 'lime.950', 'widget.divider': 'lime.900', 'widget.badgeFree': 'lime.900', 'widget.onBadgeFree': 'lime.200'}},
 'nich': {'label': 'Ніч', 'unlock': 120, 'desc': 'Глибока ніч з блакитним акцентом; однакова в обох режимах, ідеальна для StandBy.',
   'light': {'widget.background': 'neutral.950', 'widget.backgroundEnd': 'violet.950', 'widget.word': 'neutral.50', 'widget.ipa': 'neutral.300', 'widget.translation': 'neutral.300', 'widget.example': 'neutral.400', 'widget.accent': 'sky.300', 'widget.onAccent': 'sky.950', 'widget.chipBackground': 'neutral.800', 'widget.chipText': 'neutral.200', 'widget.button': 'sky.300', 'widget.onButton': 'sky.950', 'widget.divider': 'neutral.800', 'widget.badgeFree': 'neutral.800', 'widget.onBadgeFree': 'neutral.200'},
   'dark': {'widget.background': 'neutral.1000', 'widget.backgroundEnd': 'violet.950', 'widget.word': 'neutral.50', 'widget.ipa': 'neutral.300', 'widget.translation': 'neutral.300', 'widget.example': 'neutral.400', 'widget.accent': 'sky.300', 'widget.onAccent': 'sky.950', 'widget.chipBackground': 'neutral.800', 'widget.chipText': 'neutral.200', 'widget.button': 'sky.300', 'widget.onButton': 'sky.950', 'widget.divider': 'neutral.800', 'widget.badgeFree': 'neutral.800', 'widget.onBadgeFree': 'neutral.200'}},
 'sontse': {'label': 'Сонце', 'unlock': 200, 'desc': 'Тепле сонячне світло з кораловим акцентом; уночі — бурштин.',
   'light': {'widget.background': 'sunny.200', 'widget.backgroundEnd': 'sunny.400', 'widget.word': 'sunny.950', 'widget.ipa': 'sunny.800', 'widget.translation': 'sunny.800', 'widget.example': 'sunny.800', 'widget.accent': 'coral.600', 'widget.onAccent': '#FFFFFF', 'widget.chipBackground': 'sunny.400', 'widget.chipText': 'sunny.900', 'widget.button': 'sunny.950', 'widget.onButton': 'sunny.100', 'widget.divider': 'sunny.400', 'widget.badgeFree': 'sunny.400', 'widget.onBadgeFree': 'sunny.900'},
   'dark': {'widget.background': 'sunny.950', 'widget.backgroundEnd': 'neutral.950', 'widget.word': 'sunny.100', 'widget.ipa': 'sunny.300', 'widget.translation': 'sunny.300', 'widget.example': 'sunny.300', 'widget.accent': 'sunny.400', 'widget.onAccent': 'sunny.950', 'widget.chipBackground': 'sunny.900', 'widget.chipText': 'sunny.200', 'widget.button': 'sunny.400', 'widget.onButton': 'sunny.950', 'widget.divider': 'sunny.900', 'widget.badgeFree': 'sunny.900', 'widget.onBadgeFree': 'sunny.200'}},
}
for t in themes.values():
    for m in ('light', 'dark'):
        t[m + '_resolved'] = {k: resolve(v) for k, v in t[m].items()}

pairs = [
 ('text.primary', 'surface.default', 'body'), ('text.primary', 'surface.elevated', 'body'), ('text.primary', 'surface.sunken', 'body'),
 ('text.secondary', 'surface.default', 'body'), ('text.secondary', 'surface.elevated', 'body'), ('text.secondary', 'surface.sunken', 'body'),
 ('text.tertiary', 'surface.default', 'body'), ('text.tertiary', 'surface.elevated', 'body'), ('text.tertiary', 'surface.sunken', 'body'),
 ('text.link', 'surface.default', 'body'), ('text.link', 'surface.elevated', 'body'), ('text.link', 'surface.sunken', 'body'),
 ('text.inverse', 'text.primary', 'body'),
 ('brand.onPrimary', 'brand.primary', 'body'), ('brand.primary', 'surface.default', 'body'), ('brand.primary', 'surface.elevated', 'body'),
 ('brand.primaryVivid', 'surface.default', 'large'), ('brand.primaryVivid', 'surface.elevated', 'large'),
 ('brand.onPrimaryContainer', 'brand.primaryContainer', 'body'),
 ('brand.onAccent', 'brand.accent', 'body'), ('brand.accentText', 'surface.default', 'body'), ('brand.accentText', 'surface.elevated', 'body'),
 ('feedback.success', 'surface.default', 'body'), ('feedback.success', 'surface.elevated', 'body'), ('feedback.onSuccessContainer', 'feedback.successContainer', 'body'),
 ('feedback.error', 'surface.default', 'body'), ('feedback.error', 'surface.elevated', 'body'), ('feedback.onErrorContainer', 'feedback.errorContainer', 'body'),
 ('feedback.warning', 'surface.default', 'body'), ('feedback.warning', 'surface.elevated', 'body'), ('feedback.onWarningContainer', 'feedback.warningContainer', 'body'),
 ('feedback.info', 'surface.default', 'body'), ('feedback.info', 'surface.elevated', 'body'), ('feedback.onInfoContainer', 'feedback.infoContainer', 'body'),
 ('reward.goldText', 'reward.goldContainer', 'body'), ('reward.goldText', 'surface.default', 'body'), ('reward.currencyText', 'reward.currencyContainer', 'body'), ('reward.currencyText', 'surface.default', 'body'),
 ('streak.text', 'streak.container', 'body'), ('streak.text', 'surface.default', 'body'), ('streak.frozenText', 'surface.default', 'body'),
 ('quiz.correct', 'quiz.correctContainer', 'body'), ('quiz.incorrect', 'quiz.incorrectContainer', 'body'), ('quiz.selected', 'quiz.selectedContainer', 'body'),
 ('quiz.correctBorder', 'surface.elevated', 'ui'), ('quiz.incorrectBorder', 'surface.elevated', 'ui'), ('quiz.selectedBorder', 'surface.elevated', 'ui'),
 ('border.strong', 'surface.default', 'ui'), ('border.strong', 'surface.elevated', 'ui'), ('border.focus', 'surface.default', 'ui'), ('border.focus', 'surface.elevated', 'ui'),
 ('widget.word', 'widget.background', 'large'), ('widget.translation', 'widget.background', 'body'), ('widget.ipa', 'widget.background', 'body'), ('widget.example', 'widget.background', 'body'),
 ('widget.onAccent', 'widget.accent', 'body'), ('widget.chipText', 'widget.chipBackground', 'body'), ('widget.onButton', 'widget.button', 'body'), ('widget.onBadgeFree', 'widget.badgeFree', 'body'),
 ('widget.accent', 'widget.background', 'ui'), ('widget.button', 'widget.background', 'ui'),
]
thr = {'body': 4.5, 'large': 3.0, 'ui': 3.0}
rows = []; fails = []
for mode in ('light', 'dark'):
    for fg, bg, kind in pairs:
        f = SEM[mode][fg]; b = SEM[mode][bg]
        r = contrast(f, b); ok = r >= thr[kind]
        rows.append((mode, fg, bg, f, b, kind, round(r, 2), ok, r >= 7))
        if not ok: fails.append((mode, fg, bg, f, b, kind, round(r, 2)))
print('FAILS:', fails)
tpairs = [('widget.word', 'widget.background', 'large'), ('widget.word', 'widget.backgroundEnd', 'large'), ('widget.translation', 'widget.background', 'body'), ('widget.translation', 'widget.backgroundEnd', 'body'), ('widget.ipa', 'widget.background', 'body'), ('widget.example', 'widget.background', 'body'), ('widget.onAccent', 'widget.accent', 'body'), ('widget.chipText', 'widget.chipBackground', 'body'), ('widget.onButton', 'widget.button', 'body'), ('widget.accent', 'widget.background', 'ui'), ('widget.button', 'widget.background', 'ui')]
trows = []
for key, t in themes.items():
    for mode in ('light', 'dark'):
        s = dict(SEM[mode]); s.update(t[mode + '_resolved'])
        for fg, bg, kind in tpairs:
            r = contrast(s[fg], s[bg]); ok = r >= thr[kind]
            trows.append((key, mode, fg, bg, s[fg], s[bg], kind, round(r, 2), ok))
            if not ok: print('THEME FAIL', key, mode, fg, bg, s[fg], s[bg], round(r, 2))
# informational pairs (decorative, documented as not text-bearing)
info_pairs = [('light', '#B4F461', '#F7F6FC', 'lime.400 на surface.default (заливка, лише з темним чорнилом/контуром)'),
              ('light', '#FFFFFF', '#7C5CFF', 'білий на violet.500 (тому primary = violet.600)'),
              ('light', '#15131F', '#7C5CFF', 'neutral.950 на violet.500'),
              ('light', '#FFFFFF', '#FF6B6B', 'білий на coral.400 (тому error = coral.600)'),
              ('light', '#FF6B6B', '#FFFFFF', 'coral.400 як текст на білому'),
              ('light', '#FFC53D', '#FFFFFF', 'sunny.400 як текст на білому'),
              ('light', '#4CC9F0', '#FFFFFF', 'sky.400 як текст на білому'),
              ('light', '#15131F', '#FFC53D', 'neutral.950 на sunny.400 (бейдж нагороди)'),
              ('light', '#15131F', '#4CC9F0', 'neutral.950 на sky.400 (інфо-бейдж)'),
              ('light', '#15131F', '#B4F461', 'neutral.950 на lime.400 (лаймовий бейдж)')]
info_rows = [(m, f, b, d, round(contrast(f, b), 2)) for m, f, b, d in info_pairs]

# ---------------- tokens.json ----------------
def prim_ref(name):
    fam, step = name.split('.'); return '{color.primitive.%s.%s}' % (fam, step)
def sem_token(v, key):
    resolved = resolve(v)
    tok = {'$value': v if v.startswith('#') else prim_ref(v)}
    if key in DESC: tok['$description'] = DESC[key]
    tok['$extensions'] = {'org.burdock.tokens': {'resolved': resolved}}
    return tok
def nest(flat_dict, tokfn):
    out = {}
    for k, v in flat_dict.items():
        grp, name = k.split('.')
        out.setdefault(grp, {})[name] = tokfn(v, k)
    return out
tokens = {
 '$description': 'Реп’ях (Burdock) design tokens. Single source of truth. Format: W3C Design Tokens Community Group (2024 draft) with $type/$value; aliases in {curly.path} form; theme modes are separate branches (color.semantic.light / color.semantic.dark) because Style Dictionary, the XAML generator and Swift all consume explicit per-mode sets. Resolved hex values are mirrored in $extensions["org.burdock.tokens"].resolved for tools without alias support.',
 '$extensions': {'org.burdock.tokens': {'version': '0.1.0', 'spec': 'docs/specs/02-design-system.md', 'generator': 'design/tokens/generate.py', 'modes': ['light', 'dark'], 'units': {'dimension': 'pt (iOS pt / MAUI device-independent units / Android dp; font sizes map to sp on Android)', 'duration': 'ms'}, 'colorSpace': 'sRGB, 6- or 8-digit hex (#RRGGBB / #RRGGBBAA)'}},
 'color': {'$type': 'color', 'primitive': {}, 'semantic': {'light': {}, 'dark': {}}, 'theme': {'widget': {}}},
}
for fam, r in P.items():
    tokens['color']['primitive'][fam] = {str(k): {'$value': v} for k, v in r.items()}
tokens['color']['primitive']['violet']['500']['$description'] = 'Фірмовий фіолет (Q8/Q19), без змін.'
tokens['color']['primitive']['lime']['400']['$description'] = 'Фірмовий лайм (Q8/Q19), без змін. Непридатний як текст на світлому: використовуйте lime.700.'
tokens['color']['primitive']['coral']['400']['$description'] = 'Фірмовий корал (Q8/Q19), без змін. Для тексту помилок — coral.600.'
tokens['color']['primitive']['sunny']['400']['$description'] = 'Фірмовий сонячний (Q8/Q19), без змін. Для тексту — sunny.700/800.'
tokens['color']['primitive']['sky']['400']['$description'] = 'Фірмовий блакитний (Q8/Q19), без змін. Для тексту — sky.700.'
tokens['color']['primitive']['neutral']['50']['$description'] = 'Світлий фон (Q8/Q19), без змін.'
tokens['color']['primitive']['neutral']['950']['$description'] = 'Темний фон (Q8/Q19), без змін.'
tokens['color']['semantic']['light'] = nest(light, sem_token)
tokens['color']['semantic']['dark'] = nest(dark, sem_token)
for key, t in themes.items():
    tokens['color']['theme']['widget'][key] = {
        '$description': t['desc'],
        '$extensions': {'org.burdock.tokens': {'label': t['label'], 'unlockCostKoliuchky': t['unlock'], 'overrides': 'color.semantic.{mode}.widget.*', 'xamlDictionary': 'Resources/Styles/WidgetThemes/%s.xaml' % t['label'].replace('і', 'i')}},
        'light': nest(t['light'], sem_token) if t['light'] else {},
        'dark': nest(t['dark'], sem_token) if t['dark'] else {},
    }
TYPE = {
 'display.lg': ('display', 40, 46, 700, -0.02, 1.3), 'display.md': ('display', 32, 38, 700, -0.015, 1.3),
 'headline.lg': ('display', 28, 34, 600, -0.01, 1.5), 'headline.md': ('display', 24, 30, 600, -0.005, 1.5), 'headline.sm': ('text', 20, 26, 600, 0, 1.6),
 'title.lg': ('text', 18, 24, 600, 0, 1.8), 'title.md': ('text', 16, 22, 600, 0, 1.8), 'title.sm': ('text', 14, 20, 600, 0.005, 2.0),
 'body.lg': ('text', 17, 24, 400, 0, 3.1), 'body.md': ('text', 15, 22, 400, 0, 3.1), 'body.sm': ('text', 13, 18, 400, 0.005, 3.1),
 'label.lg': ('text', 16, 20, 600, 0.005, 2.0), 'label.md': ('text', 14, 18, 500, 0.01, 2.0), 'label.sm': ('text', 12, 16, 500, 0.02, 2.0),
 'word.hero': ('display', 34, 40, 700, -0.01, 1.6), 'word.ipa': ('ipa', 15, 20, 400, 0, 2.0), 'numeric.stat': ('display', 28, 32, 700, 0, 1.5),
}
tokens['typography'] = {
 'fontFamily': {'$type': 'fontFamily',
   'text': {'$value': ['Fixel Text', 'Inter', 'system-ui', 'sans-serif'], '$description': 'Основний текст. Файли FixelText-*.ttf у Resources/Fonts, alias FixelText.'},
   'display': {'$value': ['Fixel Display', 'Inter', 'system-ui', 'sans-serif'], '$description': 'Заголовки та слово-герой. Alias FixelDisplay.'},
   'ipa': {'$value': ['-apple-system', 'Roboto', 'Noto Sans', 'sans-serif'], '$description': 'Транскрипція IPA: системний шрифт, бо Fixel не містить блок IPA Extensions (перевірено cmap 2026-09-27).'}},
 'fontWeight': {'$type': 'fontWeight', 'regular': {'$value': 400}, 'medium': {'$value': 500}, 'semibold': {'$value': 600}, 'bold': {'$value': 700}},
 'scale': {'$type': 'typography'},
 'maxScale': {'$type': 'number', '$description': 'Максимальний множник Dynamic Type / Android fontScale для ролі; body дозволяє AX5 (~3.1×).'},
}
for k, (fam, size, lh, w, ls, mx) in TYPE.items():
    grp, name = k.split('.')
    tokens['typography']['scale'].setdefault(grp, {})[name] = {'$value': {'fontFamily': '{typography.fontFamily.%s}' % fam, 'fontSize': '%dpt' % size, 'lineHeight': '%dpt' % lh, 'fontWeight': w, 'letterSpacing': '%sem' % ls}}
    tokens['typography']['maxScale'].setdefault(grp, {})[name] = {'$value': mx}
tokens['typography']['features'] = {'$type': 'string', 'tabularNumbers': {'$value': 'tnum', '$description': 'Статистика й таймери; вмикається через platform handler (UIFontDescriptor / TextView.FontFeatureSettings).'}}
SP = {1: 4, 2: 8, 3: 12, 4: 16, 5: 20, 6: 24, 7: 32, 8: 40, 9: 48, 10: 56, 11: 64, 12: 80}
tokens['spacing'] = {'$type': 'dimension', **{str(k): {'$value': '%dpt' % v} for k, v in SP.items()}}
tokens['size'] = {'$type': 'dimension', 'touchTargetMin': {'$value': '44pt', '$description': '44pt iOS / 48dp Android: генератор підставляє 48 для Android.'}, 'touchTargetAndroid': {'$value': '48pt'}, 'contentMaxWidth': {'$value': '640pt'}, 'icon': {'sm': {'$value': '16pt'}, 'md': {'$value': '20pt'}, 'lg': {'$value': '24pt'}}, 'mascot': {'xs': {'$value': '32pt'}, 'sm': {'$value': '56pt'}, 'md': {'$value': '96pt'}, 'lg': {'$value': '160pt'}, 'xl': {'$value': '240pt'}}}
tokens['radius'] = {'$type': 'dimension', 'sm': {'$value': '8pt'}, 'md': {'$value': '12pt'}, 'lg': {'$value': '16pt'}, 'xl': {'$value': '24pt'}, 'full': {'$value': '999pt'}}
tokens['borderWidth'] = {'$type': 'dimension', 'hairline': {'$value': '1pt'}, 'strong': {'$value': '2pt'}, 'focus': {'$value': '3pt'}}
def shadow(x, y, blur, spread, color): return {'offsetX': '%dpt' % x, 'offsetY': '%dpt' % y, 'blur': '%dpt' % blur, 'spread': '%dpt' % spread, 'color': color}
tokens['elevation'] = {'$type': 'shadow',
 '$description': 'Рівень 0 — без тіні. У темній темі тінь слабша, а підйом передається світлішою поверхнею (surface.elevated) і 1pt контуром border.default.',
 'light': {'1': {'$value': [shadow(0, 1, 2, 0, '#15131F0F'), shadow(0, 2, 8, 0, '#15131F0F')]}, '2': {'$value': [shadow(0, 4, 16, 0, '#15131F1A')]}, '3': {'$value': [shadow(0, 12, 32, 0, '#15131F29')]}},
 'dark': {'1': {'$value': [shadow(0, 1, 2, 0, '#00000066')]}, '2': {'$value': [shadow(0, 4, 16, 0, '#00000080')]}, '3': {'$value': [shadow(0, 12, 32, 0, '#00000099')]}}}
tokens['motion'] = {
 'duration': {'$type': 'duration', 'fast': {'$value': '120ms'}, 'base': {'$value': '200ms'}, 'slow': {'$value': '320ms'}, 'celebrate': {'$value': '600ms'}},
 'easing': {'$type': 'cubicBezier', 'standard': {'$value': [0.2, 0, 0, 1]}, 'decelerate': {'$value': [0, 0, 0, 1]}, 'accelerate': {'$value': [0.3, 0, 1, 1]}, 'overshoot': {'$value': [0.34, 1.56, 0.64, 1], '$description': 'Пружинне «прилипання» Реп’яшка; не використовується при Reduce Motion.'}},
 'spring': {'$type': 'number', 'stickResponse': {'$value': 0.35}, 'stickDamping': {'$value': 0.7}},
}
os.makedirs(REPO + '/design/tokens', exist_ok=True)
with open(REPO + '/design/tokens/tokens.json', 'w', encoding='utf-8') as f:
    json.dump(tokens, f, ensure_ascii=False, indent=2); f.write('\n')

# ---------------- markdown tables ----------------
md = []
md.append('## RAMPS')
for fam, r in P.items():
    md.append('| %s | %s |' % (fam, ' | '.join('`%s` %s' % (k, v) for k, v in r.items())))
md.append('\n## SEMANTIC (light/dark with ref)')
md.append('| Токен | Light | Dark |'); md.append('|---|---|---|')
for k in light:
    md.append('| `color.%s` | `%s` %s | `%s` %s |' % (k, light[k], SEM['light'][k], dark[k], SEM['dark'][k]))
md.append('\n## CONTRAST')
md.append('| Тема | Передній план | Фон | Hex | Тип | Коеф. | AA | 7:1 |'); md.append('|---|---|---|---|---|---|---|---|')
for (mode, fg, bg, f, b, kind, r, ok, aaa) in rows:
    md.append('| %s | `%s` | `%s` | %s / %s | %s | **%.2f** | %s | %s |' % (mode, fg, bg, f, b, {'body': 'текст 4.5', 'large': 'великий 3', 'ui': 'UI 3'}[kind], r, 'pass' if ok else 'FAIL', 'так' if aaa else ''))
md.append('\n## THEMES')
md.append('| Тема | Режим | Пара | Hex | Коеф. | AA |'); md.append('|---|---|---|---|---|---|')
for (key, mode, fg, bg, f, b, kind, r, ok) in trows:
    md.append('| %s | %s | `%s` на `%s` | %s / %s | %.2f (%s) | %s |' % (key, mode, fg, bg, f, b, r, kind, 'pass' if ok else 'FAIL'))
md.append('\n## INFO')
for (m, f, b, d, r) in info_rows: md.append('| %s | %s / %s | %.2f |' % (d, f, b, r))
md.append('\n## SUMMARY: rows=%d pass=%d fail=%d aaa=%d' % (len(rows), sum(1 for r in rows if r[7]), sum(1 for r in rows if not r[7]), sum(1 for r in rows if r[8])))
open(os.path.join(HERE, 'tables.md'), 'w', encoding='utf-8').write('\n'.join(md))

# ---------------- CSS vars ----------------
css = [':root{']
for fam, r in P.items():
    for k, v in r.items(): css.append('  --color-primitive-%s-%s:%s;' % (fam, k, v))
css.append('}')
for mode in ('light', 'dark'):
    css.append('[data-theme="%s"]{' % mode)
    for k, v in SEM[mode].items(): css.append('  --color-%s:%s;' % (k.replace('.', '-'), v))
    css.append('}')
for key, t in themes.items():
    for mode in ('light', 'dark'):
        if not t[mode]: continue
        css.append('[data-theme="%s"] .wtheme-%s{' % (mode, key))
        for k, v in t[mode + '_resolved'].items(): css.append('  --color-%s:%s;' % (k.replace('.', '-'), v))
        css.append('}')
open(os.path.join(HERE, 'vars.css'), 'w', encoding='utf-8').write('\n'.join(css))
print('written', len(rows), 'rows;', 'tokens.json lines:', sum(1 for _ in open(REPO + '/design/tokens/tokens.json', encoding='utf-8')))
for fam, r in P.items(): print(fam, ' '.join('%s:%s' % (k, v) for k, v in r.items()))
