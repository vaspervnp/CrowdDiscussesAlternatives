namespace CDA.Infrastructure.Localization;

/// <summary>
/// The Greek the platform ships with, keyed by the English source string.
/// </summary>
/// <remarks>
/// <para>
/// This is the shipped starting point, not the last word: every string here can be corrected
/// through the admin screen, and a correction is never overwritten by a later run of the seed
/// (see <see cref="LocalizationService.SeedAsync"/>).
/// </para>
/// <para>
/// The key is the exact English text a view passes to the localizer, character for character. If
/// the two drift apart the string simply falls back to English — visible, not broken — which is
/// the safety net that lets this table be edited freely.
/// </para>
/// </remarks>
public static class GreekSeed
{
    public static readonly IReadOnlyDictionary<string, string> Translations =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // --- brand and navigation ---
            ["Crowd Discusses Alternatives"] = "Το Πλήθος Συζητά Εναλλακτικές",
            ["Home"] = "Αρχική",
            ["Topics"] = "Θέματα",
            ["Privacy"] = "Απόρρητο",
            ["Notifications"] = "Ειδοποιήσεις",
            ["Messages"] = "Μηνύματα",
            ["My profile"] = "Το προφίλ μου",
            ["Sign out"] = "Αποσύνδεση",
            ["Register"] = "Εγγραφή",
            ["Sign in"] = "Σύνδεση",
            ["Language"] = "Γλώσσα",
            ["English"] = "Αγγλικά",
            ["Greek"] = "Ελληνικά",
            ["Dark mode"] = "Σκοτεινό θέμα",

            // --- common actions and words ---
            ["Save"] = "Αποθήκευση",
            ["Cancel"] = "Άκυρο",
            ["Send"] = "Αποστολή",
            ["Delete"] = "Διαγραφή",
            ["Edit"] = "Επεξεργασία",
            ["Back"] = "Πίσω",
            ["Search"] = "Αναζήτηση",

            // --- home ---
            ["A place where a group works out not the one right answer, but the handful of good ones — and then weighs them against what actually matters."]
                = "Ένας χώρος όπου μια ομάδα δεν βρίσκει τη μία σωστή απάντηση, αλλά τις λίγες καλές — και μετά τις σταθμίζει με βάση αυτό που πραγματικά μετράει.",
            ["Browse topics"] = "Περιήγηση θεμάτων",
            ["Agree what matters first"] = "Συμφωνήστε πρώτα τι μετράει",
            ["A topic starts by settling the requirements a good answer has to meet, before any answer is on the table."]
                = "Ένα θέμα ξεκινά ορίζοντας τις απαιτήσεις που πρέπει να πληροί μια καλή απάντηση, προτού μπει καμία απάντηση στο τραπέζι.",
            ["Propose, don’t just comment"] = "Προτείνετε, μη σχολιάζετε απλώς",
            ["Proposals are kept apart from the discussion around them, so the real options do not get buried in the talk."]
                = "Οι προτάσεις κρατούνται ξεχωριστά από τη συζήτηση γύρω τους, ώστε οι πραγματικές επιλογές να μη χάνονται μέσα στα λόγια.",
            ["Alternatives, not one winner"] = "Εναλλακτικές, όχι ένας νικητής",
            ["Proposals are assembled into whole alternative solutions, and each is weighed against the agreed requirements."]
                = "Οι προτάσεις συντίθενται σε ολοκληρωμένες εναλλακτικές λύσεις, και καθεμία σταθμίζεται με βάση τις συμφωνημένες απαιτήσεις.",

            // --- privacy ---
            ["Your display name appears on everything you post, so that a contribution can be weighed by who made it. Nothing else about you is shown unless you choose to show it."]
                = "Το εμφανιζόμενο όνομά σας εμφανίζεται σε ό,τι δημοσιεύετε, ώστε μια συνεισφορά να σταθμίζεται με βάση το ποιος την έκανε. Τίποτα άλλο για εσάς δεν εμφανίζεται, εκτός αν επιλέξετε να το εμφανίσετε.",
            ["Every other profile field — your real name, location, email, and the rest — starts private, and you decide its audience on your profile page. Email addresses are never shown to other people by default."]
                = "Κάθε άλλο πεδίο του προφίλ — το πραγματικό σας όνομα, η τοποθεσία, το email και τα υπόλοιπα — ξεκινά ιδιωτικό, και εσείς αποφασίζετε το κοινό του στη σελίδα του προφίλ σας. Οι διευθύνσεις email δεν εμφανίζονται ποτέ σε άλλους από προεπιλογή.",
            ["What you write inside a topic is visible to everyone who can see that topic. A private message is between the two of you. Neither is sold, shared, or used for anything but running the discussion."]
                = "Ό,τι γράφετε μέσα σε ένα θέμα είναι ορατό σε όποιον βλέπει το θέμα. Ένα προσωπικό μήνυμα μένει μεταξύ σας. Τίποτα από τα δύο δεν πωλείται, δεν κοινοποιείται ούτε χρησιμοποιείται για κάτι άλλο πέρα από τη διεξαγωγή της συζήτησης.",

            // --- account ---
            ["Create an account"] = "Δημιουργία λογαριασμού",
            ["Create account"] = "Δημιουργία λογαριασμού",
            ["Email"] = "Email",
            ["Password"] = "Κωδικός πρόσβασης",
            ["Stay signed in"] = "Να παραμείνω συνδεδεμένος",
            ["I already have an account"] = "Έχω ήδη λογαριασμό",
            ["Display name"] = "Εμφανιζόμενο όνομα",
            ["Confirm password"] = "Επιβεβαίωση κωδικού",
            ["Shown on every proposal, comment and vote you make. It cannot be hidden, so choose a name you are happy to be known by."]
                = "Εμφανίζεται σε κάθε πρόταση, σχόλιο και ψήφο σας. Δεν μπορεί να αποκρυφθεί, γι’ αυτό διαλέξτε ένα όνομα που σας εκφράζει.",
            ["At least 12 characters."] = "Τουλάχιστον 12 χαρακτήρες.",
            ["An email address is required."] = "Απαιτείται διεύθυνση email.",
            ["That is not a valid email address."] = "Αυτή δεν είναι έγκυρη διεύθυνση email.",
            ["A display name is required."] = "Απαιτείται εμφανιζόμενο όνομα.",
            ["A display name is between 2 and 60 characters."] = "Το εμφανιζόμενο όνομα έχει από 2 έως 60 χαρακτήρες.",
            ["A password is required."] = "Απαιτείται κωδικός πρόσβασης.",
            ["Use at least 12 characters."] = "Χρησιμοποιήστε τουλάχιστον 12 χαρακτήρες.",
            ["The passwords do not match."] = "Οι κωδικοί δεν ταιριάζουν.",

            // --- topics: list and creation ---
            ["Start a topic"] = "Ξεκινήστε ένα θέμα",
            ["Ranked by how important participants judge them. Ranking matters here: a forum with hundreds of undifferentiated threads spreads a crowd too thin to reach a conclusion on any of them."]
                = "Ταξινομημένα κατά το πόσο σημαντικά τα κρίνουν οι συμμετέχοντες. Η ταξινόμηση μετράει εδώ: ένα φόρουμ με εκατοντάδες αδιαφοροποίητα νήματα σκορπίζει το πλήθος τόσο ώστε να μην καταλήγει σε κανένα.",
            ["Most important"] = "Πιο σημαντικά",
            ["Newest"] = "Νεότερα",
            ["No topics yet."] = "Δεν υπάρχουν θέματα ακόμη.",
            ["Discussing"] = "Σε συζήτηση",
            ["Proposing"] = "Σε προτάσεις",
            ["Closed"] = "Κλειστό",
            ["invite only"] = "μόνο με πρόσκληση",
            ["closed"] = "κλειστό",
            ["member"] = "μέλος",
            ["%count% vote"] = "%count% ψήφος",
            ["%count% votes"] = "%count% ψήφοι",
            ["This topic hides its tallies until it closes."] = "Αυτό το θέμα κρύβει τους αριθμούς μέχρι να κλείσει.",
            ["ranked, counts hidden"] = "ταξινομημένο, αριθμοί κρυμμένοι",
            ["Show more"] = "Εμφάνιση περισσότερων",
            ["Anyone can read and join"] = "Ο καθένας μπορεί να διαβάζει και να συμμετέχει",
            ["Only members I invite"] = "Μόνο μέλη που προσκαλώ",
            ["Subject"] = "Θέμα",
            ["State the problem, not a solution to it — the solutions are what the crowd will build."]
                = "Διατυπώστε το πρόβλημα, όχι μια λύση του — τις λύσεις θα τις χτίσει το πλήθος.",
            ["Description"] = "Περιγραφή",
            ["Who can read this"] = "Ποιος μπορεί να το διαβάζει",
            ["Target completion date"] = "Ημερομηνία-στόχος ολοκλήρωσης",
            ["Hide vote counts until the topic closes"] = "Απόκρυψη αριθμού ψήφων μέχρι να κλείσει το θέμα",
            ["Ranking still works; only the numbers are withheld. Seeing a running total changes how people vote."]
                = "Η ταξινόμηση εξακολουθεί να λειτουργεί· κρύβονται μόνο οι αριθμοί. Το να βλέπεις το τρέχον σύνολο αλλάζει τον τρόπο που ψηφίζουν οι άνθρωποι.",
            ["Create"] = "Δημιουργία",
            ["A subject is required."] = "Απαιτείται θέμα.",
            ["A subject is between 5 and 200 characters."] = "Το θέμα έχει από 5 έως 200 χαρακτήρες.",

            // --- notifications ---
            ["Email me"] = "Στείλε μου email",
            ["once a day, gathered together"] = "μία φορά την ημέρα, συγκεντρωμένα",
            ["as things happen"] = "μόλις συμβαίνουν",
            ["never — I will look here"] = "ποτέ — θα κοιτάζω εδώ",
            ["Email is not switched on for this installation."] = "Το email δεν είναι ενεργοποιημένο σε αυτήν την εγκατάσταση.",
            ["Nothing is being sent, whatever you choose above — but everything is still listed here, and your preference is remembered for when it is configured."]
                = "Δεν αποστέλλεται τίποτα, ό,τι κι αν επιλέξετε παραπάνω — αλλά όλα εξακολουθούν να καταγράφονται εδώ, και η προτίμησή σας θυμάται για όταν ρυθμιστεί.",
            ["Everything that happens is recorded here whatever your email setting, so turning email off costs you nothing."]
                = "Ό,τι συμβαίνει καταγράφεται εδώ ανεξάρτητα από τη ρύθμιση email, οπότε το να απενεργοποιήσετε το email δεν σας κοστίζει τίποτα.",
            ["Nothing yet."] = "Τίποτα ακόμη.",
            ["Mark everything as read"] = "Σήμανση όλων ως αναγνωσμένων",
            ["new"] = "νέο",

            // --- messages ---
            ["For the things that genuinely are between two people. Anything that bears on a topic belongs in the topic, where everyone can see it and it counts towards a conclusion."]
                = "Για όσα αφορούν πραγματικά δύο μόνο ανθρώπους. Ό,τι σχετίζεται με ένα θέμα ανήκει στο θέμα, όπου το βλέπουν όλοι και μετράει προς ένα συμπέρασμα.",
            ["No messages. You can start one from anybody's profile."] = "Κανένα μήνυμα. Μπορείτε να ξεκινήσετε από το προφίλ οποιουδήποτε.",
            ["%count% new"] = "%count% νέα",
            ["Messages with %name%"] = "Μηνύματα με %name%",
            ["read"] = "αναγνώστηκε",
            ["Write a message…"] = "Γράψτε ένα μήνυμα…",

            // --- profile ---
            ["Only me"] = "Μόνο εγώ",
            ["Signed-in members"] = "Συνδεδεμένα μέλη",
            ["Anyone"] = "Οποιοσδήποτε",
            ["Profile saved."] = "Το προφίλ αποθηκεύτηκε.",
            ["Each field has its own audience. New accounts start with everything set to only me — nothing here is shared until you choose to share it. Your display name is always visible, because it appears on everything you post."]
                = "Κάθε πεδίο έχει το δικό του κοινό. Οι νέοι λογαριασμοί ξεκινούν με τα πάντα στο «μόνο εγώ» — τίποτα εδώ δεν κοινοποιείται μέχρι να το επιλέξετε. Το εμφανιζόμενο όνομά σας είναι πάντα ορατό, γιατί εμφανίζεται σε ό,τι δημοσιεύετε.",
            ["Real name"] = "Πραγματικό όνομα",
            ["Visible to"] = "Ορατό σε",
            ["Contact email"] = "Email επικοινωνίας",
            ["Location"] = "Τοποθεσία",
            ["Website"] = "Ιστότοπος",
            ["About"] = "Σχετικά",
            ["Online status"] = "Κατάσταση σύνδεσης",
            ["Whether others can see that you are currently active."] = "Αν οι άλλοι μπορούν να δουν ότι είστε τώρα ενεργός.",
            ["View as others see it"] = "Προβολή όπως το βλέπουν οι άλλοι",
            ["online"] = "συνδεδεμένος",
            ["Member since %date%"] = "Μέλος από %date%",
            ["Edit my profile"] = "Επεξεργασία προφίλ",
            ["Send a message"] = "Στείλτε μήνυμα",

            // --- topic details ---
            ["to conclude by %date%"] = "να ολοκληρωθεί έως %date%",
            ["How important is this topic?"] = "Πόσο σημαντικό είναι αυτό το θέμα;",
            ["from %count% vote"] = "από %count% ψήφο",
            ["from %count% votes"] = "από %count% ψήφους",
            ["Tallies are hidden until this topic closes, so that seeing the running total does not shape how people vote. Ranking still applies."]
                = "Οι αριθμοί κρύβονται μέχρι να κλείσει το θέμα, ώστε το τρέχον σύνολο να μη διαμορφώνει τον τρόπο που ψηφίζουν οι άνθρωποι. Η ταξινόμηση εξακολουθεί να ισχύει.",
            ["Important"] = "Σημαντικό",
            ["Neutral"] = "Ουδέτερο",
            ["Not important"] = "Μη σημαντικό",
            ["Withdraw my vote"] = "Απόσυρση της ψήφου μου",
            ["This topic has closed."] = "Αυτό το θέμα έχει κλείσει.",
            ["%link% to vote."] = "%link% για να ψηφίσετε.",
            ["Join this topic"] = "Συμμετοχή στο θέμα",
            ["Members can add proposals and take part in the discussion."] = "Τα μέλη μπορούν να προσθέτουν προτάσεις και να συμμετέχουν στη συζήτηση.",
            ["See the proposals"] = "Δείτε τις προτάσεις",
            ["See the alternative solutions"] = "Δείτε τις εναλλακτικές λύσεις",
            ["Search the discussion"] = "Αναζήτηση στη συζήτηση",
            ["Factor tables"] = "Πίνακες παραγόντων",
            ["Requirements"] = "Απαιτήσεις",
            ["What any solution to this topic must achieve. Agreed here during the discussion, then fixed — groups of proposals are scored against this list later, so it stops changing once the topic opens for proposals."]
                = "Τι πρέπει να επιτυγχάνει κάθε λύση αυτού του θέματος. Συμφωνούνται εδώ κατά τη συζήτηση και μετά παγιώνονται — ομάδες προτάσεων βαθμολογούνται με βάση αυτόν τον κατάλογο αργότερα, γι’ αυτό παύει να αλλάζει μόλις το θέμα ανοίξει για προτάσεις.",
            ["Nothing agreed yet."] = "Δεν έχει συμφωνηθεί τίποτα ακόμη.",
            ["Move up"] = "Μετακίνηση πάνω",
            ["Move down"] = "Μετακίνηση κάτω",
            ["Remove"] = "Αφαίρεση",
            ["A solution must…"] = "Μια λύση πρέπει…",
            ["Add requirement"] = "Προσθήκη απαίτησης",
            ["Settled when this topic opened for proposals. Changing them now would invalidate evaluations already made against them."]
                = "Παγιώθηκαν όταν το θέμα άνοιξε για προτάσεις. Η αλλαγή τους τώρα θα ακύρωνε αξιολογήσεις που έχουν ήδη γίνει με βάση αυτές.",
            ["Discussion"] = "Συζήτηση",
            ["Clarify the subject and work out the requirements here. Proposals come later and stay separate from this conversation."]
                = "Διευκρινίστε το θέμα και διαμορφώστε τις απαιτήσεις εδώ. Οι προτάσεις έρχονται αργότερα και μένουν ξεχωριστά από αυτή τη συζήτηση.",
            ["No comments yet."] = "Δεν υπάρχουν σχόλια ακόμη.",
            ["Edited"] = "Επεξεργασμένο",
            ["edited"] = "επεξεργασμένο",
            ["This comment was withdrawn."] = "Αυτό το σχόλιο αποσύρθηκε.",
            ["Withdraw"] = "Απόσυρση",
            ["Add to the discussion…"] = "Προσθέστε στη συζήτηση…",
            ["Post comment"] = "Δημοσίευση σχολίου",
            ["Posting will add you to this topic."] = "Η δημοσίευση θα σας προσθέσει σε αυτό το θέμα.",
            ["%link% to take part."] = "%link% για να συμμετάσχετε.",
            ["Facilitator"] = "Συντονιστής",
            ["Open for proposals"] = "Άνοιγμα για προτάσεις",
            ["Close the topic"] = "Κλείσιμο θέματος",
            ["Phases only move forward; a closed topic cannot be reopened."] = "Οι φάσεις προχωρούν μόνο προς τα εμπρός· ένα κλειστό θέμα δεν μπορεί να ανοίξει ξανά.",

            // --- proposals: list ---
            ["Proposals — %topic%"] = "Προτάσεις — %topic%",
            ["Proposals"] = "Προτάσεις",
            ["See the alternative solutions assembled from these →"] = "Δείτε τις εναλλακτικές λύσεις που συντέθηκαν από αυτές →",
            ["The pool of building blocks for this topic. Each one is roughly a sentence, so that the parts people agree on can be reused in an alternative that fixes the parts they do not."]
                = "Η δεξαμενή δομικών στοιχείων αυτού του θέματος. Καθένα είναι περίπου μία πρόταση, ώστε τα μέρη που συμφωνούν οι άνθρωποι να επαναχρησιμοποιούνται σε μια εναλλακτική που διορθώνει όσα δεν συμφωνούν.",
            ["Most supported"] = "Με τη μεγαλύτερη στήριξη",
            ["Recently discussed"] = "Πρόσφατα συζητημένες",
            ["Fold duplicates together"] = "Ενοποίηση διπλότυπων",
            ["when agreed by at least"] = "όταν συμφωνούνται από τουλάχιστον",
            ["Apply"] = "Εφαρμογή",
            ["Showing only proposals by %name%."] = "Εμφάνιση μόνο προτάσεων του/της %name%.",
            ["Show everyone's"] = "Εμφάνιση όλων",
            ["No proposals yet."] = "Δεν υπάρχουν προτάσεις ακόμη.",
            ["%count% comment"] = "%count% σχόλιο",
            ["%count% comments"] = "%count% σχόλια",
            ["Still open for improvement, so it cannot be voted on yet."] = "Ακόμη ανοιχτή για βελτίωση, οπότε δεν μπορεί να ψηφιστεί ακόμη.",
            ["editable"] = "επεξεργάσιμη",
            ["Folded together with proposals the crowd reported as duplicates."] = "Ενοποιημένη με προτάσεις που το πλήθος ανέφερε ως διπλότυπες.",
            ["+%count% duplicate"] = "+%count% διπλότυπο",
            ["+%count% duplicates"] = "+%count% διπλότυπα",
            ["voting not open"] = "η ψηφοφορία δεν έχει ανοίξει",
            ["combined across duplicates"] = "συνδυασμένο σε όλα τα διπλότυπα",
            ["counts hidden"] = "αριθμοί κρυμμένοι",
            ["Add a proposal"] = "Προσθήκη πρότασης",
            ["One sentence — a single building block, not a whole solution."] = "Μία πρόταση — ένα δομικό στοιχείο, όχι μια ολόκληρη λύση.",
            ["Up to %count% characters. If your idea needs more, it is more than one proposal — split it, so people can accept the parts they agree with."]
                = "Έως %count% χαρακτήρες. Αν η ιδέα σας χρειάζεται περισσότερους, είναι πάνω από μία πρόταση — χωρίστε την, ώστε οι άνθρωποι να δέχονται τα μέρη που συμφωνούν.",
            ["Stay editable for"] = "Να παραμείνει επεξεργάσιμη για",
            ["3 days (default)"] = "3 ημέρες (προεπιλογή)",
            ["1 day"] = "1 ημέρα",
            ["7 days"] = "7 ημέρες",
            ["14 days"] = "14 ημέρες",
            ["Add proposal"] = "Προσθήκη πρότασης",
            ["While it stays editable you can improve it in response to comments, and nobody can vote on it yet. You can end that window early at any time."]
                = "Όσο παραμένει επεξεργάσιμη μπορείτε να τη βελτιώνετε απαντώντας σε σχόλια, και κανείς δεν μπορεί ακόμη να την ψηφίσει. Μπορείτε να κλείσετε αυτό το διάστημα νωρίτερα ανά πάσα στιγμή.",
            ["This topic is still agreeing its requirements. Proposals open once the facilitator has published them."]
                = "Αυτό το θέμα συμφωνεί ακόμη τις απαιτήσεις του. Οι προτάσεις ανοίγουν μόλις ο συντονιστής τις δημοσιεύσει.",

            // --- proposal details ---
            ["Proposal"] = "Πρόταση",
            ["Proposals for %topic%"] = "Προτάσεις για %topic%",
            ["Still open for improvement."] = "Ακόμη ανοιχτή για βελτίωση.",
            ["The author can change the wording until %date% UTC, so voting is not open yet — a vote cast now would attach to wording that changes underneath it. Comments are welcome, and are how the author finds out what to improve."]
                = "Ο συντάκτης μπορεί να αλλάξει τη διατύπωση μέχρι %date% UTC, οπότε η ψηφοφορία δεν έχει ανοίξει ακόμη — μια ψήφος τώρα θα προσκολλούνταν σε διατύπωση που αλλάζει από κάτω της. Τα σχόλια είναι ευπρόσδεκτα, και έτσι μαθαίνει ο συντάκτης τι να βελτιώσει.",
            ["Improve the wording"] = "Βελτιώστε τη διατύπωση",
            ["Finish editing and open voting"] = "Ολοκλήρωση επεξεργασίας και άνοιγμα ψηφοφορίας",
            ["This cannot be undone — once people have voted, the wording is what they voted on."]
                = "Αυτό δεν αναιρείται — μόλις ψηφίσουν οι άνθρωποι, η διατύπωση είναι αυτό που ψήφισαν.",
            ["Do you support this proposal?"] = "Στηρίζετε αυτή την πρόταση;",
            ["Voting opens when the author finishes editing."] = "Η ψηφοφορία ανοίγει όταν ο συντάκτης ολοκληρώσει την επεξεργασία.",
            ["This topic withholds its tallies until it closes."] = "Αυτό το θέμα κρύβει τους αριθμούς του μέχρι να κλείσει.",
            ["Support"] = "Στήριξη",
            ["Oppose"] = "Αντίθεση",
            ["Similar proposals"] = "Παρόμοιες προτάσεις",
            ["Nobody has reported this as a duplicate of anything."] = "Κανείς δεν την έχει αναφέρει ως διπλότυπο κάποιας άλλης.",
            ["Enough agreement to fold at this topic's default threshold."] = "Αρκετή συμφωνία ώστε να ενοποιηθεί στο προεπιλεγμένο όριο αυτού του θέματος.",
            ["active"] = "ενεργό",
            ["reported by"] = "αναφέρθηκε από",
            ["judged the better written of the two"] = "κρίθηκε η καλύτερα διατυπωμένη από τις δύο",
            ["Are these the same?"] = "Είναι ίδιες;",
            ["Yes"] = "Ναι",
            ["Unsure"] = "Αβέβαιο",
            ["No"] = "Όχι",
            ["You agree these two say the same thing, but you voted %here% on this one and %there% on the other. That splits the support for a single idea across two entries. Consider voting the same on both."]
                = "Συμφωνείτε ότι αυτές οι δύο λένε το ίδιο, αλλά ψηφίσατε %here% σε αυτήν και %there% στην άλλη. Αυτό διασπά τη στήριξη μιας μόνο ιδέας σε δύο καταχωρίσεις. Σκεφτείτε να ψηφίσετε το ίδιο και στις δύο.",
            ["Report a duplicate"] = "Αναφορά διπλότυπου",
            ["Paste the id of the other proposal from its address bar. Nothing is merged — you are recording a claim that others can agree or disagree with."]
                = "Επικολλήστε το αναγνωριστικό της άλλης πρότασης από τη γραμμή διεύθυνσής της. Τίποτα δεν συγχωνεύεται — καταγράφετε έναν ισχυρισμό με τον οποίο άλλοι μπορούν να συμφωνήσουν ή να διαφωνήσουν.",
            ["Other proposal id"] = "Αναγνωριστικό άλλης πρότασης",
            ["Why do you think they say the same thing? (optional)"] = "Γιατί πιστεύετε ότι λένε το ίδιο; (προαιρετικό)",
            ["No preference on wording"] = "Καμία προτίμηση στη διατύπωση",
            ["This one is better written"] = "Αυτή είναι καλύτερα διατυπωμένη",
            ["Report as similar"] = "Αναφορά ως παρόμοια",
            ["Sources"] = "Πηγές",
            ["Each source is judged on two separate questions, because they come apart: something can be entirely accurate and beside the point, or highly relevant and unreliable."]
                = "Κάθε πηγή κρίνεται σε δύο ξεχωριστά ερωτήματα, γιατί διαφέρουν: κάτι μπορεί να είναι απολύτως ακριβές και άσχετο, ή πολύ σχετικό και αναξιόπιστο.",
            ["Nothing cited yet."] = "Καμία αναφορά ακόμη.",
            ["cited by"] = "αναφέρθηκε από",
            ["also supports %count% other proposal"] = "στηρίζει επίσης %count% ακόμη πρόταση",
            ["also supports %count% other proposals"] = "στηρίζει επίσης %count% ακόμη προτάσεις",
            ["Is it accurate?"] = "Είναι ακριβές;",
            ["Does it matter here?"] = "Έχει σημασία εδώ;",
            ["Cite a source"] = "Αναφορά πηγής",
            ["What is it? e.g. 2024 city council traffic study, pages 12–18"] = "Τι είναι; π.χ. μελέτη κυκλοφορίας δήμου 2024, σελίδες 12–18",
            ["Add source"] = "Προσθήκη πηγής",
            ["If this source is already cited elsewhere in the topic, it will be linked rather than duplicated — so its rating stays in one place."]
                = "Αν αυτή η πηγή αναφέρεται ήδη αλλού στο θέμα, θα συνδεθεί αντί να διπλασιαστεί — ώστε η βαθμολογία της να μένει σε ένα μέρος.",
            ["Attached files"] = "Συνημμένα αρχεία",
            ["Nothing attached."] = "Κανένα συνημμένο.",
            ["Attach"] = "Επισύναψη",
            ["Up to %count% MB. Documents, spreadsheets, images, PDFs and plain text. If it is already on the web, cite it as a source instead — a link keeps its context."]
                = "Έως %count% MB. Έγγραφα, υπολογιστικά φύλλα, εικόνες, PDF και απλό κείμενο. Αν είναι ήδη στο διαδίκτυο, αναφέρετέ το ως πηγή — ένας σύνδεσμος διατηρεί το πλαίσιό του.",
            ["Comments"] = "Σχόλια",
            ["Add a comment…"] = "Προσθέστε ένα σχόλιο…",
            ["Suggest an improvement…"] = "Προτείνετε μια βελτίωση…",

            // --- alternative solutions (groups) ---
            ["Alternative solutions — %topic%"] = "Εναλλακτικές λύσεις — %topic%",
            ["Alternative solutions"] = "Εναλλακτικές λύσεις",
            ["My comparison"] = "Η σύγκρισή μου",
            ["Assemble an alternative"] = "Συνθέστε μια εναλλακτική",
            ["Each of these is a set of proposals taken together — a complete answer built from the shared pool. Two alternatives that differ on one point share everything else, so the disagreement is visible instead of buried in two competing essays."]
                = "Καθεμία από αυτές είναι ένα σύνολο προτάσεων μαζί — μια πλήρης απάντηση χτισμένη από την κοινή δεξαμενή. Δύο εναλλακτικές που διαφέρουν σε ένα σημείο μοιράζονται όλα τα υπόλοιπα, ώστε η διαφωνία να είναι ορατή αντί θαμμένη σε δύο ανταγωνιστικά κείμενα.",
            ["%label% alternatives from %names% — the participants whose cited sources this topic rates most highly."]
                = "%label% εναλλακτικές από %names% — τους συμμετέχοντες των οποίων τις πηγές αυτό το θέμα εκτιμά περισσότερο.",
            ["Listed first:"] = "Πρώτα στη λίστα:",
            ["Nothing assembled yet. Once there are proposals in the pool, anyone can combine them into an alternative."]
                = "Δεν έχει συντεθεί τίποτα ακόμη. Μόλις υπάρχουν προτάσεις στη δεξαμενή, ο καθένας μπορεί να τις συνδυάσει σε μια εναλλακτική.",
            ["%count% proposal"] = "%count% πρόταση",
            ["%count% proposals"] = "%count% προτάσεις",
            ["Among this topic's best-regarded citers of sources."] = "Ανάμεσα στους πιο έγκυρους παραθέτες πηγών αυτού του θέματος.",
            ["well-sourced"] = "καλά τεκμηριωμένη",
            ["A refinement of another alternative rather than a fresh answer."] = "Μια βελτίωση άλλης εναλλακτικής παρά μια νέα απάντηση.",
            ["variant"] = "παραλλαγή",
            ["This will be marked as a %label% of: %target%"] = "Αυτή θα σημειωθεί ως %label% της: %target%",
            ["Marking refinements as such keeps the list readable — six alternatives that are adjustments of two approaches is a different picture from six unrelated answers."]
                = "Η σήμανση των βελτιώσεων ως τέτοιων κρατά τη λίστα ευανάγνωστη — έξι εναλλακτικές που είναι προσαρμογές δύο προσεγγίσεων είναι διαφορετική εικόνα από έξι άσχετες απαντήσεις.",
            ["Pick the proposals that, taken together, make up your answer. The order does not matter — it is a set, not a list."]
                = "Επιλέξτε τις προτάσεις που, μαζί, συνθέτουν την απάντησή σας. Η σειρά δεν έχει σημασία — είναι σύνολο, όχι λίστα.",
            ["What does this combination amount to?"] = "Σε τι ισοδυναμεί αυτός ο συνδυασμός;",
            ["Why these proposals belong together, and what the result achieves."] = "Γιατί αυτές οι προτάσεις πάνε μαζί, και τι επιτυγχάνει το αποτέλεσμα.",
            ["Required. A bare list leaves everyone guessing at the reasoning that picked these, which is most of what tells one alternative from another."]
                = "Υποχρεωτικό. Μια σκέτη λίστα αφήνει τους πάντες να μαντεύουν το σκεπτικό που τις διάλεξε, που είναι το κύριο που ξεχωρίζει τη μία εναλλακτική από την άλλη.",
            ["Proposals in the pool"] = "Προτάσεις στη δεξαμενή",
            ["The pool is empty. Add some proposals first."] = "Η δεξαμενή είναι άδεια. Προσθέστε πρώτα κάποιες προτάσεις.",
            ["still editable"] = "ακόμη επεξεργάσιμη",
            ["Create alternative"] = "Δημιουργία εναλλακτικής",
            ["At least two proposals — a single one is already votable on its own."] = "Τουλάχιστον δύο προτάσεις — μία μόνο ψηφίζεται ήδη από μόνη της.",
            ["Alternative solution"] = "Εναλλακτική λύση",
            ["Alternative solutions for %topic%"] = "Εναλλακτικές λύσεις για %topic%",
            ["assembled by"] = "συντέθηκε από",
            ["A variant of"] = "Παραλλαγή της",
            ["The %count% proposal in this alternative"] = "Η %count% πρόταση σε αυτή την εναλλακτική",
            ["The %count% proposals in this alternative"] = "Οι %count% προτάσεις σε αυτή την εναλλακτική",
            ["%count% comment on this proposal"] = "%count% σχόλιο σε αυτή την πρόταση",
            ["%count% comments on this proposal"] = "%count% σχόλια σε αυτή την πρόταση",
            ["Do you support this alternative?"] = "Στηρίζετε αυτή την εναλλακτική;",
            ["Evaluate against the requirements"] = "Αξιολόγηση ως προς τις απαιτήσεις",
            ["A private working-out, for you alone, before you decide how to vote."] = "Μια ιδιωτική επεξεργασία, μόνο για εσάς, πριν αποφασίσετε πώς θα ψηφίσετε.",
            ["Build a variant of this"] = "Δημιουργήστε μια παραλλαγή της",
            ["Disagree with part of it? Start from this combination and change what you would do differently, rather than rejecting the whole thing."]
                = "Διαφωνείτε με ένα μέρος της; Ξεκινήστε από αυτόν τον συνδυασμό και αλλάξτε ό,τι θα κάνατε διαφορετικά, αντί να απορρίψετε το σύνολο.",
            ["Reword the description"] = "Επαναδιατυπώστε την περιγραφή",
            ["%count% person has already voted on this alternative. Rewriting it now changes what they judged — if you are changing what it proposes rather than clarifying it, %link% instead."]
                = "%count% άτομο έχει ήδη ψηφίσει αυτή την εναλλακτική. Η αναδιατύπωσή της τώρα αλλάζει αυτό που έκριναν — αν αλλάζετε αυτό που προτείνει αντί να το διευκρινίζετε, %link% αντ’ αυτού.",
            ["%count% people have already voted on this alternative. Rewriting it now changes what they judged — if you are changing what it proposes rather than clarifying it, %link% instead."]
                = "%count% άτομα έχουν ήδη ψηφίσει αυτή την εναλλακτική. Η αναδιατύπωσή της τώρα αλλάζει αυτό που έκριναν — αν αλλάζετε αυτό που προτείνει αντί να το διευκρινίζετε, %link% αντ’ αυτού.",
            ["build a variant"] = "δημιουργήστε μια παραλλαγή",
            ["Comments on this alternative"] = "Σχόλια για αυτή την εναλλακτική",
            ["Comment on this combination…"] = "Σχολιάστε αυτόν τον συνδυασμό…",

            // --- evaluation ---
            ["Evaluate an alternative"] = "Αξιολόγηση εναλλακτικής",
            ["Evaluate this alternative"] = "Αξιολογήστε αυτή την εναλλακτική",
            ["Score how well this combination satisfies each requirement the topic agreed on, weighted by how much each requirement matters to you. This is a tool for thinking before you vote — %emphasis%."]
                = "Βαθμολογήστε πόσο καλά αυτός ο συνδυασμός ικανοποιεί κάθε απαίτηση που συμφώνησε το θέμα, σταθμισμένη με το πόσο σας ενδιαφέρει καθεμία. Είναι εργαλείο σκέψης πριν ψηφίσετε — %emphasis%.",
            ["nobody else can see it"] = "κανείς άλλος δεν το βλέπει",
            ["Requirement"] = "Απαίτηση",
            ["How much it matters"] = "Πόσο μετράει",
            ["to you, across this topic"] = "για εσάς, σε όλο το θέμα",
            ["How well this meets it"] = "Πόσο καλά το ικανοποιεί",
            ["this alternative only"] = "μόνο αυτή η εναλλακτική",
            ["0 — not at all"] = "0 — καθόλου",
            ["5 — decisive"] = "5 — καθοριστικό",
            ["5 — fully"] = "5 — πλήρως",
            ["Weights carry across this topic."] = "Οι βαρύτητες ισχύουν σε όλο το θέμα.",
            ["Changing how much a requirement matters to you also changes it in every other alternative you evaluate here. That is what lets the totals be compared with each other — if each alternative had its own weights, the scores would be on different scales and comparing them would mean nothing."]
                = "Αλλάζοντας πόσο σας ενδιαφέρει μια απαίτηση, την αλλάζετε και σε κάθε άλλη εναλλακτική που αξιολογείτε εδώ. Αυτό επιτρέπει να συγκρίνονται τα σύνολα μεταξύ τους — αν κάθε εναλλακτική είχε τις δικές της βαρύτητες, οι βαθμολογίες θα ήταν σε διαφορετικές κλίμακες και η σύγκρισή τους δεν θα σήμαινε τίποτα.",
            ["You last evaluated this on %date% UTC — scoring %score%. Re-evaluating replaces that."]
                = "Την τελευταία φορά την αξιολογήσατε στις %date% UTC — με βαθμολογία %score%. Η επαναξιολόγηση την αντικαθιστά.",
            ["Save my evaluation"] = "Αποθήκευση της αξιολόγησής μου",
            ["Compare all alternatives"] = "Σύγκριση όλων των εναλλακτικών",
            ["Compare alternatives"] = "Σύγκριση εναλλακτικών",
            ["Every alternative scored against the topic's requirements, under your weights. %emphasis%"]
                = "Κάθε εναλλακτική βαθμολογημένη ως προς τις απαιτήσεις του θέματος, με τις δικές σας βαρύτητες. %emphasis%",
            ["Only you can see this."] = "Μόνο εσείς το βλέπετε.",
            ["This topic has no requirements to evaluate against."] = "Αυτό το θέμα δεν έχει απαιτήσεις προς αξιολόγηση.",
            ["You have not evaluated any alternative yet. Open one and score it against the requirements; the results will line up here."]
                = "Δεν έχετε αξιολογήσει καμία εναλλακτική ακόμη. Ανοίξτε μία και βαθμολογήστε την ως προς τις απαιτήσεις· τα αποτελέσματα θα εμφανιστούν εδώ.",
            ["Weight"] = "Βαρύτητα",
            ["%points% pts"] = "%points% πόντοι",
            ["Total"] = "Σύνολο",
            ["%percent%% of the best possible"] = "%percent%% του καλύτερου δυνατού",
            ["The percentage divides your total by what a perfect answer would score under the same weights. A raw total on its own is hard to read — whether 34 is good depends on how many requirements there are and how heavily you weighted them."]
                = "Το ποσοστό διαιρεί το σύνολό σας με ό,τι θα βαθμολογούσε μια τέλεια απάντηση με τις ίδιες βαρύτητες. Ένα σκέτο σύνολο δύσκολα διαβάζεται — το αν το 34 είναι καλό εξαρτάται από το πλήθος των απαιτήσεων και πόσο βαριά τις σταθμίσατε.",
            ["Not yet evaluated"] = "Δεν έχουν αξιολογηθεί ακόμη",

            // --- factor tables ---
            ["Factor tables — %topic%"] = "Πίνακες παραγόντων — %topic%",
            ["Make a table"] = "Φτιάξτε έναν πίνακα",
            ["A grid of how the key factors in this problem push on each other. Solutions usually fail not because a measure does not work, but because it works while damaging something else that mattered — this is a place to write that down and see it."]
                = "Ένα πλέγμα του πώς οι βασικοί παράγοντες αυτού του προβλήματος πιέζουν ο ένας τον άλλον. Οι λύσεις συνήθως αποτυγχάνουν όχι επειδή ένα μέτρο δεν λειτουργεί, αλλά επειδή λειτουργεί ενώ βλάπτει κάτι άλλο που μετρούσε — εδώ το καταγράφετε και το βλέπετε.",
            ["Each person builds their own. A shared table is one participant's reading of the problem, offered to the topic, not an agreed fact — so tables are never merged and always carry their author's name."]
                = "Κάθε άτομο χτίζει τον δικό του. Ένας κοινός πίνακας είναι η ανάγνωση ενός συμμετέχοντα για το πρόβλημα, προσφερόμενη στο θέμα, όχι συμφωνημένο γεγονός — γι’ αυτό οι πίνακες δεν συγχωνεύονται ποτέ και φέρουν πάντα το όνομα του συντάκτη τους.",
            ["Nothing here yet."] = "Τίποτα εδώ ακόμη.",
            ["%count% factor"] = "%count% παράγοντας",
            ["%count% factors"] = "%count% παράγοντες",
            ["private to you"] = "ιδιωτικός σε εσάς",
            ["shared"] = "κοινός",
            ["Make a factor table"] = "Φτιάξτε έναν πίνακα παραγόντων",
            ["What is this table about?"] = "Για τι είναι αυτός ο πίνακας;",
            ["e.g. How the traffic factors pull against each other"] = "π.χ. Πώς οι παράγοντες κυκλοφορίας αντιτίθενται μεταξύ τους",
            ["The key factors, one per line"] = "Οι βασικοί παράγοντες, ένας ανά γραμμή",
            ["e.g. Journey time, Air quality, Cost to residents, Shop takings — one per line"] = "π.χ. Χρόνος διαδρομής, Ποιότητα αέρα, Κόστος για κατοίκους, Έσοδα καταστημάτων — ένας ανά γραμμή",
            ["Between 2 and %max% factors. The grid is square, so twelve factors already means well over a hundred judgements and a table nobody can read across — deciding which are genuinely key is part of the exercise."]
                = "Από 2 έως %max% παράγοντες. Το πλέγμα είναι τετράγωνο, οπότε δώδεκα παράγοντες σημαίνουν ήδη πάνω από εκατό κρίσεις και έναν πίνακα που κανείς δεν διαβάζει — το να αποφασίσετε ποιοι είναι πραγματικά βασικοί είναι μέρος της άσκησης.",
            ["Create the table"] = "Δημιουργία πίνακα",
            ["It starts private to you. You can share it with the topic whenever you like."] = "Ξεκινά ιδιωτικός σε εσάς. Μπορείτε να τον μοιραστείτε με το θέμα όποτε θέλετε.",
            ["Factor tables for %topic%"] = "Πίνακες παραγόντων για %topic%",
            ["by %name%"] = "από %name%",
            ["shared with the topic"] = "κοινός με το θέμα",
            ["updated %date% UTC"] = "ενημερώθηκε %date% UTC",
            ["Read a row as %quote% and the columns as the factors affected. The point is to notice where pushing on something that helps one part of the problem quietly damages another."]
                = "Διαβάστε μια γραμμή ως %quote% και τις στήλες ως τους παράγοντες που επηρεάζονται. Το ζητούμενο είναι να παρατηρήσετε πού το να πιέζετε κάτι που βοηθά ένα μέρος του προβλήματος βλάπτει σιωπηλά ένα άλλο.",
            ["\"if this factor increases, what happens to…\""] = "«αν αυτός ο παράγοντας αυξηθεί, τι συμβαίνει στο…»",
            ["strongly helps"] = "βοηθά έντονα",
            ["helps"] = "βοηθά",
            ["no effect"] = "καμία επίδραση",
            ["harms"] = "βλάπτει",
            ["strongly harms"] = "βλάπτει έντονα",
            ["This table has no factors."] = "Αυτός ο πίνακας δεν έχει παράγοντες.",
            ["An increase in…"] = "Μια αύξηση στο…",
            ["…affects %factor%"] = "…επηρεάζει %factor%",
            ["why?"] = "γιατί;",
            ["Save the table"] = "Αποθήκευση πίνακα",
            ["Stop sharing this table"] = "Διακοπή κοινής χρήσης",
            ["Share this table with the topic"] = "Κοινή χρήση με το θέμα",
            ["Everyone in the topic can read it. It stays yours — nobody else can change it."] = "Όλοι στο θέμα μπορούν να τον διαβάσουν. Παραμένει δικός σας — κανείς άλλος δεν μπορεί να τον αλλάξει.",
            ["Only you can see it at the moment."] = "Μόνο εσείς τον βλέπετε προς το παρόν.",

            // --- search ---
            ["Search — %topic%"] = "Αναζήτηση — %topic%",
            ["Searches everything people have written in this topic. Combine words with %and% and %or%, group them with brackets, quote a phrase, or exclude a word with a leading minus."]
                = "Ψάχνει σε ό,τι έχουν γράψει οι άνθρωποι σε αυτό το θέμα. Συνδυάστε λέξεις με %and% και %or%, ομαδοποιήστε με παρενθέσεις, βάλτε μια φράση σε εισαγωγικά, ή αποκλείστε μια λέξη με μπροστινό μείον.",
            ["e.g. pros OR cons  ·  toll AND (bus OR tram)  ·  \"congestion charge\""] = "π.χ. υπέρ OR κατά  ·  διόδια AND (λεωφορείο OR τραμ)  ·  «τέλος συμφόρησης»",
            ["Show me"] = "Δείξε μου",
            ["proposals whose comments match"] = "προτάσεις των οποίων τα σχόλια ταιριάζουν",
            ["the matching comments"] = "τα σχόλια που ταιριάζουν",
            ["Written by"] = "Γράφτηκε από",
            ["anyone"] = "οποιονδήποτε",
            ["Ignored %terms% — words shorter than %min% characters are not indexed and cannot be searched for."]
                = "Αγνοήθηκαν %terms% — λέξεις μικρότερες από %min% χαρακτήρες δεν ευρετηριάζονται και δεν αναζητούνται.",
            ["commented on by %name%"] = "σχολιασμένες από %name%",
            ["Nothing matched."] = "Καμία αντιστοιχία.",
            ["%count% matching comment"] = "%count% σχόλιο που ταιριάζει",
            ["%count% matching comments"] = "%count% σχόλια που ταιριάζουν",
            ["on"] = "σε",
            ["in the topic discussion"] = "στη συζήτηση του θέματος",
            ["Using comments as tags"] = "Χρήση σχολίων ως ετικέτες",
            ["Because this searches comments and can return the proposals they are attached to, a marker word in a comment works as a label. Write %pros% or %cons% in your comments as you read through the pool, then come back here and search for them to pull out everything you marked. Restrict the search to your own comments and the labels are effectively private to you."]
                = "Επειδή αυτό ψάχνει σχόλια και μπορεί να επιστρέψει τις προτάσεις στις οποίες ανήκουν, μια λέξη-δείκτης σε ένα σχόλιο λειτουργεί ως ετικέτα. Γράψτε %pros% ή %cons% στα σχόλιά σας καθώς διαβάζετε τη δεξαμενή, μετά γυρίστε εδώ και αναζητήστε τα για να βγάλετε ό,τι σημειώσατε. Περιορίστε την αναζήτηση στα δικά σας σχόλια και οι ετικέτες γίνονται ουσιαστικά ιδιωτικές.",

            // --- error ---
            ["Error"] = "Σφάλμα",
            ["Something went wrong while handling your request."] = "Κάτι πήγε στραβά κατά την επεξεργασία του αιτήματός σας.",
            ["Request ID:"] = "Αναγνωριστικό αιτήματος:",
            ["If it keeps happening, quoting the request ID above will help whoever looks into it."] = "Αν επαναλαμβάνεται, η αναφορά του παραπάνω αναγνωριστικού θα βοηθήσει όποιον το εξετάσει.",

            // --- translation admin ---
            ["Translations"] = "Μεταφράσεις",
            ["Every phrase in the interface, with its %language% translation. English is the source, so it is not edited here — change a translation and it takes effect at once."]
                = "Κάθε φράση της διεπαφής, με τη μετάφρασή της στα %language%. Τα Αγγλικά είναι η πηγή, γι’ αυτό δεν αλλάζουν εδώ — αλλάξτε μια μετάφραση και ισχύει αμέσως.",
            ["%done% of %total% translated"] = "%done% από %total% μεταφρασμένα",
            ["%count% still missing"] = "%count% εκκρεμούν",
            ["Show all"] = "Εμφάνιση όλων",
            ["Show only what is missing"] = "Εμφάνιση μόνο όσων λείπουν",
            ["Saved “%key%”."] = "Αποθηκεύτηκε «%key%».",
            ["Nothing to show."] = "Τίποτα προς εμφάνιση.",
            ["(untranslated — shows English)"] = "(αμετάφραστο — εμφανίζει Αγγλικά)",
        };
}
