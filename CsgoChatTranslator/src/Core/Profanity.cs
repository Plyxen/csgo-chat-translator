using System.Text;
using System.Text.RegularExpressions;

namespace CsgoChatTranslator;

/// <summary>
/// Literal, unsoftened meanings of Russian swear words, insults and slurs, for moderation.
/// Machine translation tends to tone these down or drop them, so they are looked up here instead.
/// Each word is matched against the lists in order; the first hit wins, so specific forms come first.
/// Common ways of dodging filters are undone first: с*ка, с у к а, п1дор, cyка, суууука.
/// </summary>
public static partial class Profanity
{
    private const int MaxShown = 4;
    private const RegexOptions Options = RegexOptions.CultureInvariant | RegexOptions.Compiled;

    [GeneratedRegex(@"[\p{L}\p{Nd}@*]+")]
    private static partial Regex Word();

    [GeneratedRegex(@"(?<![\p{L}\p{Nd}])(?:\p{L}[ ._\-]){2,}\p{L}(?![\p{L}\p{Nd}])")]
    private static partial Regex SpelledOut();

    [GeneratedRegex(@"(.)\1+")]
    private static partial Regex Repeats();

    private static (Regex Pattern, string Meaning)[] Build(params (string Pattern, string Meaning)[] entries) =>
        [.. entries.Select(e => (new Regex(e.Pattern, Options), e.Meaning))];

    // Matched against lower-case words with ё already replaced by е.
    private static readonly (Regex Pattern, string Meaning)[] Cyrillic = Build(
        // хуй
        (@"^(иди)?наху[йя]$|^нах$|^пнх$|^пшнх$", "fuck off / go fuck yourself"),
        (@"^поху[йюя]\w*|^пох$", "don't give a fuck"),
        (@"^(ни|до)ху[яй]\w*", "нихуя = fuck all, дохуя = a fuckload"),
        (@"^хз$", "'хуй знает': fuck knows"),
        (@"^хуйл\w*", "dickhead"),
        (@"^хуйн\w*|^хует\w*", "bullshit / fucking crap"),
        (@"^хуе(в|во|вый|вая|вое|вые)\w*$", "shitty / fucking awful"),
        (@"^хуесос\w*|^членосос\w*|^вафл(ер|ист)\w*", "cocksucker"),
        (@"^[оа]хуе\w*|^[оа]хуи\w*", "fucking insane / gone too fucking far"),
        (@"^(за|от|при|вы|на|по)?хуяр\w*", "to fucking hit / do (vulgar)"),
        (@"^(на|по|до|ни|о|а|за|от|при|вы)?ху[йеяюи]\w*", "dick / cock (vulgar)"),
        // пизда
        (@"^пиздец\w*|^пздц$|^пзд$", "totally fucked / fucking disaster"),
        (@"^пизд(еж|ишь|ит|ят|ел|ела|ели|еть|ун\w*|абол\w*)$", "bullshitting / fucking liar"),
        (@"^пизд(а|ы|у|е|ой)$|^манд(а|ы|у|ой|авошк\w*)$", "cunt"),
        (@"^(от|вы|рас|из)пизд\w*", "to beat the shit out of"),
        (@"^спизд\w*", "stole (vulgar)"),
        (@"\w*пизд\w*", "vulgar root 'cunt'"),
        // ебать
        (@"^долб[оа]е[бп]\w*|^далбаеб\w*", "dumbfuck / fuckwit"),
        (@"^уеб(ок|ки|ка|ку|ан\w*|ищ\w*)\w*$", "motherfucker / ugly fuck"),
        (@"^еблан\w*", "fucking idiot"),
        (@"^еб(ло|ал[оа]|альник\w*|лище)$", "fucking face / mouth ('shut your fucking mouth')"),
        (@"^заеб\w*", "pissing me the fuck off / fed up"),
        (@"^от[ъь]?еб\w*|^с[ъь]еб\w*", "fuck off / get the fuck out"),
        (@"^наеб\w*|^об[ъь]?еб\w*", "to con / fuck over"),
        (@"^еб(ан|уч)\w*", "fucking (adjective)"),
        (@"^(й?епт(а|ь|ы)?|йопт(а|ь)?)$", "clipped 'ёб твою мать': fuck!"),
        (@"^(вы|у|до|про|раз[ъь]?|по|в[ъь])?еб(а|у|л|и|ы|о|е|н|ш|$)\w*", "to fuck (vulgar)"),
        (@"^(вы|от|по|за)?трах\w*", "to screw / fuck (milder)"),
        // блядь
        (@"^бля(т|д)?ь?$|^блеа$|^блэт$|^мля$|^бл$|^бль$", "'whore', used like 'fuck!'"),
        (@"^(вы|у|за|раз|по)?бляд\w*|^блят\w*", "whore / fucking (vulgar)"),
        // sexual and body
        (@"^су(к[аиуое]|чк|чар|чий|чь|чонок)\w*|^сцук\w*", "bitch"),
        (@"^шлю[хш]\w*|^шалав\w*|^шмар\w*|^проститут\w*|^давалк\w*|^курв\w*", "whore / slut"),
        (@"^шкур(а|ы|у)$", "'hide': gold-digging slut"),
        (@"^стерв\w*", "bitch (nasty woman)"),
        (@"^соск(а|и|у)$", "'pacifier': cocksucker / slut"),
        (@"^(от|по)?сос(и|ите|ешь|ал|ать|ни)$|^отсос\w*|^минет\w*", "suck (my dick) / blowjob"),
        (@"^(по|за|на)?дроч\w*|^задрот\w*", "to jerk off / wanker"),
        (@"^залуп\w*", "dickhead (lit. glans)"),
        (@"^(на|по|ни|до)?хер(а|у|ом|ня|ов\w*|н\w*)?$", "dick (milder), 'нахер' = fuck off"),
        (@"^(на|по|ни)?хрен(а|у|ь|ов\w*)?$", "mild 'dick' / 'what the hell'"),
        (@"^член(а|ом|у|ы)?$", "penis (also just 'member')"),
        (@"^пис(ьк|юн|ечк)\w*", "dick / pussy (childish)"),
        (@"^сис(ьк|ек|ечк)\w*", "tits"),
        (@"^очк(о|а|у|ом|ошник\w*)$", "asshole (anus) / coward; can also mean 'a point'"),
        (@"^жоп\w*|^срак\w*", "ass"),
        (@"^(на|за|об[оа]|по|вы|про)?сра[тлнк]\w*", "to shit / 'I don't give a shit'"),
        (@"^(об[оа]|на|по|за)?сса[тл]\w*|^об[оа]сс\w*|^ссык(л|ун)\w*|^ссанин\w*", "to piss / pissed-on / coward"),
        (@"^(на|про)?перд\w*", "to fart / old fart"),
        (@"^говн\w*|^дерьм\w*", "shit"),
        (@"^фак(ин\w*|ер\w*|аю|ю)?$|^мазафак\w*", "fuck (English loanword)"),
        // homophobic
        (@"^пид[оа]р\w*|^пидр\w*|^педик\w*|^педр\w*|^пидик\w*", "faggot (homophobic slur)"),
        (@"^гом(ик|осек)\w*|^глиномес\w*|^заднеприв\w*", "homophobic slur"),
        (@"^пету(х|шар|шок)\w*|^опущен\w*", "prison slur: bitch / faggot"),
        // insults
        (@"^муд(ак|ил|озвон)\w*", "asshole"),
        (@"^г[оа]ндон\w*", "'condom': scumbag"),
        (@"^мраз(ь|и|ота|ей)\w*|^сволоч\w*|^подон\w*|^гнид\w*|^падл\w*", "scum / bastard"),
        (@"^твар(ь|и|ью|ей|ина)$", "'creature': scumbag / bitch"),
        (@"^ублюд\w*|^выродо?к\w*|^недонос\w*", "bastard"),
        (@"^урод(ы|а|ина|ец|ка|ов|у)?$", "freak / ugly bastard"),
        (@"^чмо(шник\w*|шка)?$|^чмыр\w*|^ушлеп\w*", "worthless lowlife / dipshit"),
        (@"^коз(ел|лы|ла|лу|лина)$", "'goat': bastard (strong insult)"),
        (@"^черт(и|а|ила|ей)?$", "'devil': lowlife (prison insult), also mild 'damn'"),
        (@"^лох(а|и|у|ом|ов|ушк\w*)?$|^лош(ар|ок|пед)\w*|^терпил\w*", "sucker / loser"),
        (@"^д[еиэ]бил\w*|^кретин\w*|^идиот\w*|^имбецил\w*|^олигофрен\w*|^дегенерат\w*|^недоум\w*", "moron / retard"),
        (@"^даун\w*", "'Down's': retard (ableist slur)"),
        (@"^аутист\w*|^шиз(ик\w*|а|оид\w*)?$", "'autist' / 'schizo' used as an insult"),
        (@"^отстал(ый|ая|ые)$|^конч(ен|енн)(ый|ая|ые|ого)$", "retarded / total degenerate"),
        (@"^долбан\w*", "nutcase (softened 'fucked in the head')"),
        (@"^туп(ой|ая|ые|иц\w*|орыл\w*)$|^безмозгл\w*", "stupid / dumbass"),
        (@"^дур(ак\w*|а|ы|ень|ачок|очка|ища)$|^придур\w*", "fool / idiot"),
        (@"^баран(ы|а|у|ов|ина)?$|^ос(ел|лы|ла)$|^овц(а|ы|у|е)$|^олен(ь|и|ей)$", "animal insult: stubborn / clueless idiot"),
        (@"^свин(ья|ьи|ью|ота)$|^скотин\w*|^животн(ое|ые|ых)$|^псин\w*|^шавк\w*", "animal insult: pig / brute / mutt"),
        (@"^крыс(а|ы|у|ой|ятнич\w*)$", "'rat': backstabber / camper"),
        (@"^рак(и|ов|ом|а)?$|^дн(о|ище)$", "gamer insult: hopeless player (also 'cancer' / 'bottom')"),
        (@"^школот\w*|^сопл(як|ячк)\w*", "'schoolkid' / snotty brat"),
        (@"^быдл\w*|^отброс\w*|^нищ(ий|ая|ие|еброд\w*|ук\w*)$|^бомж\w*", "trash / broke-ass loser / bum"),
        (@"^дрищ\w*", "scrawny shit"),
        (@"^мамк(у|а|и|е|ой)$", "'your mom' (usually part of a mother insult)"),
        // threats
        // "сдох" and "убью" on their own are normal talk in a shooter, so only the forms aimed at a person.
        (@"^(с|по)дохн(и|ите|ешь|ете)$", "drop dead"),
        (@"^умри(те)?$|^повес(ься|ись)$", "die / hang yourself"),
        // ethnic, racist and political slurs
        (@"^ниг+ер\w*|^нигг?а$", "the N-word"),
        (@"^негр\w*", "'negr': Black person (in Russian not always meant as a slur)"),
        (@"^черно(ж|маз)\w*|^обезьян\w*|^мартышк\w*|^макак\w*", "racist slur / 'monkey'"),
        (@"^хох(ол|лы|ла|лу|лов|лам|лом|луш\w*|лятск\w*|лин\w*)$|^укроп(ы|ов|а|ам)?$|^бандер(а|ы|у|овец|овц\w*|лог\w*)$", "slur / hostile label for Ukrainians"),
        (@"^кацап\w*|^москал\w*|^русн(я|и|ю|е|ей)$|^ватник\w*|^рашист\w*", "slur / hostile label for Russians"),
        (@"^свинособак\w*", "'pig-dog': dehumanising slur"),
        (@"^чурк\w*|^чурбан\w*|^чучмек\w*|^нерус\w*", "slur for Central Asians / Caucasians / non-Russians"),
        (@"^хач(и|а|ей|ик\w*|ам|ами|у|ом|е)?$|^армяшк\w*", "slur for people from the Caucasus"),
        (@"^жид(ы|а|у|ов|ам|ом|овк\w*|яр\w*|омасон\w*)?$", "kike (antisemitic slur)"),
        (@"^узкоглаз\w*|^китаез\w*|^япошк\w*", "racist slur for Asians"),
        (@"^п[ие]ндос\w*|^америкос\w*", "slur for Americans"),
        (@"^пшек\w*|^бульбаш\w*", "slur for Poles / Belarusians"),
        (@"^азер(ы|а|ов|бот\w*)?$", "derogatory for Azerbaijanis"),
        (@"^зиг(а|у|и|овать|ую|уй)?$|^хайль$|^гитлер\w*", "Nazi salute / reference"));

    // Russian typed with Latin letters, matched against lower-case words.
    private static readonly (Regex Pattern, string Meaning)[] Latin = Build(
        (@"^(idi)?na(hu[iy]|xu[iy]|hyi|xyi|huj|xyu)$|^nax$", "нахуй: fuck off / go fuck yourself"),
        (@"^po(hu[iy]|xu[iy]|hyi|xyi|huj|xyu)\w*", "похуй: don't give a fuck"),
        (@"^(hu[iy]|xu[iy]|hyi|xyi|huj|xyu)(nya|na|a|u|e|i|lo)?$", "хуй: dick / cock, 'хуйня' = bullshit"),
        (@"^(huesos|xuesos|hyesos)\w*", "хуесос: cocksucker"),
        (@"^pi[zs]de[cz]\w*|^pzdc$", "пиздец: totally fucked"),
        (@"^pi[zs]d\w*", "пизда: cunt (vulgar root)"),
        (@"^dolb[oa][ey]?[eo]b\w*", "долбоёб: dumbfuck"),
        (@"^u[ey]?[eo]b(ok|an|ki|ishe)\w*", "уёбок: motherfucker"),
        (@"^eblan\w*", "еблан: fucking idiot"),
        (@"^za[ey]?eb\w*", "заебал: pissing me the fuck off"),
        (@"^(ot|s)[y']?eb(is|al|i)\w*", "отъебись / съебись: fuck off"),
        (@"^na[ey]?eb\w*", "наебал: conned / fucked over"),
        (@"^([yj]?eba[tl]|[yj]?eban|[yj]?ebuch|[yj]?ebl[oa])\w*", "ебать: to fuck / fucking"),
        (@"^(blya[td]?|bl[ij]a[td]|blyt|bljat|bljad|blet)\w*", "блять: 'whore', used like 'fuck!'"),
        (@"^(suka|cyka|syka|suki|suchk|sucha)\w*", "сука: bitch"),
        (@"^(pid[oa]r|pidr|pedik)\w*", "пидор: faggot (homophobic slur)"),
        (@"^petu[hx]\w*", "петух: prison slur, bitch / faggot"),
        (@"^mudak\w*|^mudil\w*", "мудак: asshole"),
        (@"^g[oa]ndon\w*", "гандон: scumbag (lit. condom)"),
        (@"^zalup\w*", "залупа: dickhead"),
        (@"^(shl(u|y|yu|ju)[hx]|wluh|shalav)\w*|^kur[vw]a$", "шлюха / курва: whore"),
        (@"^mraz\w*", "мразь: scum"),
        (@"^ubl[yu]+dok\w*", "ублюдок: bastard"),
        (@"^chmo(shnik)?$", "чмо: worthless lowlife"),
        (@"^debil\w*|^durak\w*|^tupo[yi]$", "дебил / дурак / тупой: moron, fool, stupid"),
        (@"^daun(y|i|a|u)?$", "даун: retard (ableist slur)"),
        (@"^(ot)?sosi$", "соси: suck (my dick)"),
        (@"^(na|po)h[ue]r$", "нахер: fuck off (milder)"),
        (@"^govn\w*", "говно: shit"),
        (@"^(zh|j)opa$", "жопа: ass"),
        (@"^lo[hx]$", "лох: sucker / loser"),
        (@"^mamk[ua]$", "мамку: 'your mom' (mother insult)"),
        (@"^sdohni$", "сдохни: drop dead"),
        (@"^(hohol|hohl[iy]|xoxol|xoxl[iy])\w*", "хохол: slur for Ukrainians"),
        (@"^churk\w*", "чурка: slur for Central Asians / Caucasians"));

    // What a censored word like с*ка or х** most likely stands for, most common first.
    private static readonly string[] CensoredCyrillic =
    [
        "сука", "суки", "блять", "блядь", "бля", "хуй", "хуя", "нахуй", "хуйня", "пиздец", "пизда", "пизды",
        "пидор", "пидорас", "похуй", "ебать", "ебал", "ебаный", "ебаная", "заебал", "мудак", "говно", "жопа", "шлюха", "долбоеб",
        "уебок", "гандон", "залупа", "нахер", "дебил", "нигер", "хохол",
    ];

    private static readonly string[] CensoredLatin = ["suka", "cyka", "blyat", "pidor", "nahui", "nahuy", "pizdec", "ebat"];

    // Latin letters and digits used in place of Cyrillic look-alikes.
    private static readonly Dictionary<char, char> LookAlikes = new()
    {
        ['a'] = 'а', ['b'] = 'в', ['c'] = 'с', ['e'] = 'е', ['h'] = 'н', ['k'] = 'к', ['m'] = 'м', ['o'] = 'о',
        ['p'] = 'р', ['t'] = 'т', ['x'] = 'х', ['y'] = 'у', ['u'] = 'и', ['n'] = 'п', ['i'] = 'и',
        ['0'] = 'о', ['3'] = 'з', ['4'] = 'ч', ['6'] = 'б', ['1'] = 'и', ['@'] = 'а',
    };

    /// <summary>"word = meaning" for each flagged word, as typed, or null when the text is clean.</summary>
    public static string? Describe(string text)
    {
        List<string>? found = null;
        var seen = new HashSet<string>(StringComparer.Ordinal);

        // "с у к а" and "п.и.д.о.р" are read as one word.
        var prepared = SpelledOut().Replace(text, m => string.Concat(m.Value.Where(char.IsLetter)));

        foreach (Match word in Word().Matches(prepared))
        {
            var meaning = Lookup(word.Value);
            if (meaning is null || !seen.Add(word.Value.ToLowerInvariant())) continue;

            found ??= [];
            if (found.Count < MaxShown) found.Add($"{word.Value} = {meaning}");
            else if (found.Count == MaxShown) found.Add("…");
        }
        return found is null ? null : string.Join("   ·   ", found);
    }

    private static string? Lookup(string raw)
    {
        var word = raw.ToLowerInvariant().Replace('ё', 'е').TrimStart('@');
        if (word.Length == 0) return null;

        if (word.All(char.IsDigit)) return word == "1488" ? "neo-Nazi code" : null;
        if (!word.Any(char.IsLetter)) return null;

        if (ChatParser.HasCyrillicLetter(word))
        {
            word = MapLookAlikes(word);
            return word.Contains('*') ? Censored(word, CensoredCyrillic, Cyrillic, "[а-я]{1,5}") : Match(word, Cyrillic);
        }

        if (word.Contains('*')) return Censored(word, CensoredLatin, Latin, "[a-z]{1,3}");

        var latin = word.Replace('1', 'i').Replace('0', 'o').Replace('3', 'e').Replace('4', 'a').Replace('@', 'a');
        var meaning = Match(latin, Latin);
        if (meaning is not null) return meaning;

        // A word written entirely in look-alike letters, such as "cyka" or "xyeta".
        return word.All(LookAlikes.ContainsKey) ? Match(MapLookAlikes(word), Cyrillic) : null;
    }

    private static string? Match(string word, (Regex Pattern, string Meaning)[] list)
    {
        var collapsed = Repeats().Replace(word, "$1");
        foreach (var candidate in collapsed == word ? [word] : new[] { word, collapsed })
        {
            foreach (var (pattern, meaning) in list)
            {
                if (pattern.IsMatch(candidate)) return meaning;
            }
        }
        return null;
    }

    private static string? Censored(string word, string[] candidates, (Regex Pattern, string Meaning)[] list, string anyLetters)
    {
        // Censoring keeps the first letter, so "*да*" style emphasis is not mistaken for it.
        if (word.Length < 3 || !char.IsLetter(word[0])) return null;

        var pattern = new StringBuilder("^");
        var lastWasStar = false;
        foreach (var c in word)
        {
            if (c == '*')
            {
                if (!lastWasStar) pattern.Append(anyLetters);
                lastWasStar = true;
            }
            else
            {
                pattern.Append(Regex.Escape(c.ToString()));
                lastWasStar = false;
            }
        }
        pattern.Append('$');

        var guess = candidates.FirstOrDefault(c => Regex.IsMatch(c, pattern.ToString(), RegexOptions.CultureInvariant));
        return guess is null ? null : $"censored, probably '{guess}': {Match(guess, list)}";
    }

    private static string MapLookAlikes(string word)
    {
        var sb = new StringBuilder(word.Length);
        foreach (var c in word) sb.Append(LookAlikes.TryGetValue(c, out var mapped) ? mapped : c);
        return sb.ToString();
    }
}
