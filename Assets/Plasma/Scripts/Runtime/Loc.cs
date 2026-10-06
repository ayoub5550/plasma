using System.Collections.Generic;

namespace Plasma
{
    /// <summary>
    /// UI strings in English and Arabic. T(key, args) formats and, for Arabic, shapes + reorders
    /// the text for display (ArabicText). Add every new UI string here in both languages.
    /// </summary>
    public static class Loc
    {
        public static bool Arabic;

        static readonly Dictionary<string, string[]> S = new Dictionary<string, string[]>
        {
            { "title", new[] { "PLASMA", "بلازما" } },
            { "subtitle", new[] { "SQUAD  vs  HORDE", "الفرقة ضد الحشد" } },
            { "level", new[] { "LEVEL {0}", "المستوى {0}" } },
            { "wave", new[] { "WAVE {0}", "الموجة {0}" } },
            { "play", new[] { "PLAY", "العب" } },
            { "endless", new[] { "ENDLESS", "بلا نهاية" } },
            { "best_wave", new[] { "BEST WAVE {0}", "أفضل موجة {0}" } },
            { "upg0", new[] { "FIREPOWER", "قوة النار" } },
            { "upg1", new[] { "FIRE RATE", "سرعة الإطلاق" } },
            { "upg2", new[] { "START SQUAD", "جنود البداية" } },
            { "upg3", new[] { "TILE BONUS", "مكافأة البطاقات" } },
            { "lv", new[] { "LV {0}", "مستوى {0}" } },
            { "max", new[] { "MAX", "الأقصى" } },
            { "paused", new[] { "PAUSED", "إيقاف مؤقت" } },
            { "resume", new[] { "RESUME", "متابعة" } },
            { "menu", new[] { "MENU", "القائمة" } },
            { "settings", new[] { "SETTINGS", "الإعدادات" } },
            { "sound", new[] { "SOUND", "الأصوات" } },
            { "music", new[] { "MUSIC", "الموسيقى" } },
            { "vibration", new[] { "VIBRATION", "الاهتزاز" } },
            { "language", new[] { "LANGUAGE", "اللغة" } },
            { "on", new[] { "ON", "تشغيل" } },
            { "off", new[] { "OFF", "إيقاف" } },
            { "lang_name", new[] { "ENGLISH", "العربية" } },
            { "close", new[] { "CLOSE", "إغلاق" } },
            { "victory", new[] { "VICTORY!", "انتصار!" } },
            { "defeat", new[] { "DEFEAT", "هزيمة" } },
            { "game_over", new[] { "GAME OVER", "انتهت اللعبة" } },
            { "next", new[] { "NEXT", "التالي" } },
            { "retry", new[] { "RETRY", "أعد المحاولة" } },
            { "squad_left", new[] { "SQUAD LEFT {0}", "الجنود الباقون {0}" } },
            { "kills", new[] { "KILLS {0}", "القتلى {0}" } },
            { "max_squad", new[] { "MAX SQUAD {0}", "أكبر فرقة {0}" } },
            { "progress", new[] { "PROGRESS {0}%", "التقدم {0}%" } },
            { "tip_upgrades", new[] { "TIP: BUY UPGRADES!", "نصيحة: اشترِ الترقيات!" } },
            { "new_best", new[] { "NEW BEST!", "رقم قياسي جديد!" } },
            { "boss_level", new[] { "BOSS LEVEL", "مستوى الزعيم" } },
            { "boss", new[] { "BOSS!", "الزعيم!" } },
            { "hint_drag", new[] { "DRAG TO MOVE", "اسحب لتحريك الفرقة" } },
            { "hint_gate", new[] { "SHOOT THE GATE!", "أطلق النار على البوابة!" } },
            { "hint_collect", new[] { "COLLECT THE TILES!", "اجمع البطاقات!" } },
            { "hint_horde", new[] { "STOP THE HORDE!", "أوقف الحشد!" } },
            { "hint_boss", new[] { "KILL THE BOSS!", "اقضِ على الزعيم!" } },
            { "upgrades", new[] { "UPGRADES", "الترقيات" } },
            { "coins_earned", new[] { "+ {0}", "+ {0}" } },
        };

        public static string T(string key, params object[] args)
        {
            if (!S.TryGetValue(key, out var pair)) return key;
            string s = pair[Arabic ? 1 : 0];
            if (args != null && args.Length > 0) s = string.Format(s, args);
            return Arabic ? ArabicText.Fix(s) : s;
        }
    }
}
