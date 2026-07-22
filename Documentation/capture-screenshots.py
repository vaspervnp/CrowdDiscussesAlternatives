"""Seeds a realistic scenario and captures the screenshots used by Documentation/manual.md.

Run against a local dev instance with an empty database. Re-runnable: the database is
cleared first.
"""
import os
import pathlib
import sys
from playwright.sync_api import sync_playwright

BASE = "http://localhost:5105"
OUT = pathlib.Path(r"C:\Git\CrowdDiscussesAlternatives\Documentation\images")
OUT.mkdir(parents=True, exist_ok=True)

PASSWORD = "a long spoken passphrase"

# Supplied by the environment; never hard-coded here.
DB_HOST = os.environ["CDA_DB_HOST"]
DB_USER = os.environ["CDA_DB_USER"]
DB_PASSWORD = os.environ["CDA_DB_PASSWORD"]
DB_NAME = os.environ.get("CDA_DB_NAME", "CrowdDiscussesAlternatives")


def register(page, email, display_name):
    # Sign out first: the nav bar's own submit button would otherwise be the one clicked.
    sign_out(page)
    page.goto(f"{BASE}/Account/Register")
    page.fill("#Email", email)
    page.fill("#DisplayName", display_name)
    page.fill("#Password", PASSWORD)
    page.fill("#ConfirmPassword", PASSWORD)
    page.click("main form button[type=submit]")
    page.wait_for_load_state("networkidle")


def sign_out(page):
    page.goto(f"{BASE}/")
    btn = page.query_selector("form[action*='Logout'] button")
    if btn:
        btn.click()
        page.wait_for_load_state("networkidle")


def sign_in(page, email):
    sign_out(page)
    page.goto(f"{BASE}/Account/Login")
    page.fill("#Email", email)
    page.fill("#Password", PASSWORD)
    page.click("main form button[type=submit]")
    page.wait_for_load_state("networkidle")


def shot(page, name, full=True):
    path = OUT / f"{name}.png"
    page.screenshot(path=str(path), full_page=full)
    print(f"  wrote {path.name}")


def shot_region(page, selector, name):
    """One section of a page, for when a full-page shot would repeat an earlier one."""
    path = OUT / f"{name}.png"
    page.locator(selector).screenshot(path=str(path))
    print(f"  wrote {path.name}")


def create_topic(page, subject, description, hide_counts=False):
    page.goto(f"{BASE}/topics/create")
    page.fill("#Subject", subject)
    page.fill("#Description", description)
    if hide_counts:
        page.check("#HideVoteCountsUntilClose")
    page.click("main form button[type=submit]")
    page.wait_for_load_state("networkidle")
    return page.url


def add_requirement(page, topic_url, text):
    page.goto(topic_url)
    page.fill("form[action$='/requirements'] input[name=text]", text)
    page.click("form[action$='/requirements'] button")
    page.wait_for_load_state("networkidle")


def comment(page, topic_url, body):
    page.goto(topic_url)
    page.fill("textarea[name=body]", body)
    page.click("form[action$='/comments'] button")
    page.wait_for_load_state("networkidle")


def vote(page, topic_url, value):
    page.goto(topic_url)
    page.click(f"button[name=value][value='{value}']")
    page.wait_for_load_state("networkidle")


def add_proposal(page, topic_url, text, editable_days=None):
    page.goto(f"{topic_url}/proposals")
    page.fill("form:has(textarea[name=text]) textarea[name=text]", text)
    if editable_days is not None:
        page.select_option("#editableForDays", str(editable_days))
    page.click("form:has(textarea[name=text]) button")
    page.wait_for_load_state("networkidle")


def proposal_urls(page, topic_url):
    """Newest first, so the order matches the order they were added in reverse."""
    page.goto(f"{topic_url}/proposals?sort=Newest")
    links = page.query_selector_all("main .list-group-item a[href*='/proposals/']")
    return [link.get_attribute("href") for link in links]


def lock_proposal(page, proposal_url):
    page.goto(f"{BASE}{proposal_url}")
    page.click("form[action$='/lock'] button")
    page.wait_for_load_state("networkidle")


def vote_on_proposal(page, proposal_url, value):
    page.goto(f"{BASE}{proposal_url}")
    page.click(f"button[name=value][value='{value}']")
    page.wait_for_load_state("networkidle")


def comment_on_proposal(page, proposal_url, body):
    page.goto(f"{BASE}{proposal_url}")
    page.fill("textarea[name=body]", body)
    page.click("form[action$='/comments'] button")
    page.wait_for_load_state("networkidle")


def cite(page, proposal_url, url, description):
    page.goto(f"{BASE}{proposal_url}")
    page.fill("input[name=url]", url)
    page.fill("input[name=description]", description)
    page.click("form[action$='/references'] button")
    page.wait_for_load_state("networkidle")


def rate(page, proposal_url, reference_index, aspect, value):
    """Rates the reference at the given position on one of its two axes."""
    page.goto(f"{BASE}{proposal_url}")
    forms = page.query_selector_all("form[action$='/vote'][action*='/references/']")
    # Two forms per reference: accuracy first, then importance.
    index = reference_index * 2 + (0 if aspect == "Accuracy" else 1)
    forms[index].query_selector(f"button[value='{value}']").click()
    page.wait_for_load_state("networkidle")


def assemble(page, topic_url, description, proposal_indexes):
    """Builds an alternative from the proposals at the given positions in the pool."""
    page.goto(f"{topic_url}/alternatives/assemble")
    page.fill("#description", description)
    boxes = page.query_selector_all("input[name=proposalIds]")
    for index in proposal_indexes:
        boxes[index].check()
    page.click("main form button[type=submit]")
    page.wait_for_load_state("networkidle")


def alternative_url(page, topic_url, text_fragment):
    """Finds an alternative by its wording.

    Not by position: the list puts the best-regarded citers' alternatives first, so index
    order does not follow creation order.
    """
    page.goto(f"{topic_url}/alternatives")
    for link in page.query_selector_all("main .list-group-item a[href*='/alternatives/']"):
        if text_fragment in link.inner_text():
            return link.get_attribute("href")
    raise AssertionError(f"no alternative matching {text_fragment!r}")


def vote_on_group(page, group_url, value):
    page.goto(f"{BASE}{group_url}")
    page.click(f"button[name=value][value='{value}']")
    page.wait_for_load_state("networkidle")


def comment_on_group(page, group_url, body):
    page.goto(f"{BASE}{group_url}")
    page.fill("textarea[name=body]", body)
    page.click("form[action$='/comments'] button")
    page.wait_for_load_state("networkidle")


def evaluate(page, topic_url, group_url, weights, scores):
    """Fills in the private evaluation form for one alternative."""
    group_id = group_url.rsplit("/", 1)[-1]
    page.goto(f"{topic_url}/evaluate/{group_id}")
    weight_selects = page.query_selector_all("select[name=weights]")
    score_selects = page.query_selector_all("select[name=scores]")
    for select, value in zip(weight_selects, weights):
        select.select_option(str(value))
    for select, value in zip(score_selects, scores):
        select.select_option(str(value))
    page.click("main form button[type=submit]")
    page.wait_for_load_state("networkidle")


def build_factor_table(page, topic_url, name, factors, cells):
    """Creates a factor table and fills in its grid.

    `cells` maps (row, column) zero-based positions to (effect, note).
    """
    page.goto(f"{topic_url}/factors/new")
    page.fill("#name", name)
    page.fill("#factors", chr(10).join(factors))
    page.click("main form button[type=submit]")
    page.wait_for_load_state("networkidle")

    table_url = page.url
    selects = page.query_selector_all("select[name=effects]")
    notes = page.query_selector_all("input[name=notes]")

    # The grid renders row by row, skipping the diagonal, so a cell's position in the flat
    # list is its row offset minus the diagonals already passed.
    size = len(factors)
    for (row, column), (effect, note) in cells.items():
        index = row * (size - 1) + (column if column < row else column - 1)
        selects[index].select_option(effect)
        if note:
            notes[index].fill(note)

    page.click("form:has(select[name=effects]) button")
    page.wait_for_load_state("networkidle")
    return table_url


def reset_database():
    """Start from an empty database so the captures are reproducible.

    The tables are read from the schema rather than listed here. A hand-written list went
    stale twice — a phase adds a table, nobody remembers to add it, and the next run seeds
    on top of the last one's leftovers.
    """
    import pymysql
    con = pymysql.connect(host=DB_HOST, port=3306, user=DB_USER, password=DB_PASSWORD,
                          ssl={"ssl_mode": "REQUIRED"}, database=DB_NAME, connect_timeout=15)
    cur = con.cursor()
    cur.execute(
        "SELECT table_name FROM information_schema.tables "
        "WHERE table_schema = %s AND table_type = 'BASE TABLE' "
        "AND table_name <> '__EFMigrationsHistory'", (DB_NAME,))
    tables = [row[0] for row in cur.fetchall()]

    # Foreign keys are cyclic across the model, so the truncations cannot be ordered.
    cur.execute("SET FOREIGN_KEY_CHECKS = 0")
    for t in tables:
        cur.execute(f"TRUNCATE TABLE `{t}`")
    cur.execute("SET FOREIGN_KEY_CHECKS = 1")
    con.commit()
    con.close()
    print(f"database cleared ({len(tables)} tables)")


def main():
    reset_database()
    with sync_playwright() as p:
        browser = p.chromium.launch(channel="msedge")
        context = browser.new_context(viewport={"width": 1280, "height": 900})
        page = context.new_page()

        print("registering people")
        register(page, "chair@example.com", "Chair")

        print("creating topics")
        songs = create_topic(
            page,
            "Which 12 songs should go on the new album?",
            "Fifty candidates, twelve slots. Looking for alternative track lists rather "
            "than one definitive answer.",
        )
        traffic = create_topic(
            page,
            "How should we reduce traffic in the city centre?",
            "Alternative approaches welcome. Nothing is ruled out before it is discussed.",
            hide_counts=True,
        )

        print("capturing the create form")
        page.goto(f"{BASE}/topics/create")
        page.fill("#Subject", "How should we reduce traffic in the city centre?")
        page.fill("#Description", "Alternative approaches welcome.")
        shot(page, "topic-create")

        print("running the discussion")
        comment(page, songs, "Do live recordings count as candidates, or studio versions only?")
        add_requirement(page, songs, "Total running time must stay under 50 minutes")
        add_requirement(page, songs, "At least three tracks from the early period")
        add_requirement(page, songs, "No more than two tracks from any single album")

        register(page, "editor@example.com", "Editor")
        comment(page, songs, "Live versions should count — some of them are the better take.")
        vote(page, songs, 1)
        vote(page, traffic, 1)

        register(page, "listener@example.com", "Listener")
        comment(page, songs, "Agreed on live takes. I would add a rule about running order later.")
        vote(page, songs, 1)
        vote(page, traffic, -1)

        print("capturing the topic during its discussion phase")
        sign_in(page, "chair@example.com")
        page.goto(songs)
        shot(page, "topic-discussing")

        print("capturing the ranked topic list")
        page.goto(f"{BASE}/topics")
        shot(page, "topics-list")

        print("opening the topic for proposals")
        page.goto(songs)
        page.click("button[name=phase][value='Proposing']")
        page.wait_for_load_state("networkidle")
        shot(page, "topic-proposing")

        print("building the pool of proposals")
        add_proposal(page, songs, "Open with the earliest single, in its original mix", 7)
        add_proposal(page, songs, "Include at least one instrumental track")
        add_proposal(page, songs, "Close with the longest track rather than a short one")

        # Already registered during the discussion phase above.
        sign_in(page, "editor@example.com")
        add_proposal(page, songs, "Leave out anything already on a compilation")

        # Newest first, so index 0 is the proposal added last.
        urls = proposal_urls(page, songs)
        compilation, closing, instrumental, opening = urls[0], urls[1], urls[2], urls[3]

        comment_on_proposal(page, instrumental, "Which one, though? There are only two candidates.")

        # Two are finished and open to votes; the opening track is still being worked on.
        # Only an author can lock their own proposal, and both of these are Chair's.
        sign_in(page, "chair@example.com")
        lock_proposal(page, instrumental)
        lock_proposal(page, closing)
        vote_on_proposal(page, instrumental, 1)
        vote_on_proposal(page, closing, -1)

        sign_in(page, "listener@example.com")
        vote_on_proposal(page, instrumental, 1)
        vote_on_proposal(page, closing, 1)
        comment_on_proposal(page, opening, "The original mix is much better than the remaster.")

        print("citing sources and rating them")
        cite(page, instrumental,
             "https://example.com/liner-notes-1971",
             "Liner notes from the 1971 pressing, which list the session players")
        cite(page, instrumental,
             "example.com/interview?utm_source=newsletter",
             "1998 interview where the band discuss the instrumental")

        sign_in(page, "editor@example.com")
        # Accurate but beside the point, and relevant but shaky — the judgement the two
        # separate axes exist to express.
        rate(page, instrumental, 0, "Accuracy", 1)
        rate(page, instrumental, 0, "Importance", -1)
        rate(page, instrumental, 1, "Accuracy", -1)
        rate(page, instrumental, 1, "Importance", 1)

        sign_in(page, "listener@example.com")
        rate(page, instrumental, 0, "Accuracy", 1)
        rate(page, instrumental, 1, "Importance", 1)

        print("capturing a proposal with rated sources")
        sign_in(page, "chair@example.com")
        page.goto(f"{BASE}{instrumental}")
        shot(page, "proposal-references")

        print("reporting a duplicate and agreeing with it")
        sign_in(page, "chair@example.com")
        add_proposal(page, songs, "Start the record with the first single we released")
        duplicate = proposal_urls(page, songs)[0]
        lock_proposal(page, duplicate)

        # Report it as saying the same thing as the earlier opening-track proposal, and judge
        # the earlier wording the better of the two.
        page.goto(f"{BASE}{duplicate}")
        page.fill("input[name=otherProposalId]", opening.rsplit("/", 1)[-1])
        page.fill("input[name=justification]", "Both say the album should open with the first single.")
        page.click("form[action$='/similar'] button")
        page.wait_for_load_state("networkidle")

        page.click("form[action*='/similar/'] button[value='1']")
        page.wait_for_load_state("networkidle")

        sign_in(page, "editor@example.com")
        page.goto(f"{BASE}{duplicate}")
        page.click("form[action*='/similar/'] button[value='1']")
        page.wait_for_load_state("networkidle")

        print("capturing a proposal reported as a duplicate")
        sign_in(page, "chair@example.com")
        page.goto(f"{BASE}{duplicate}")
        shot(page, "proposal-similar")

        print("capturing the proposal pool")
        page.goto(f"{songs}/proposals")
        shot(page, "proposals-list")

        print("capturing the pool with duplicates folded together")
        page.goto(f"{songs}/proposals?collapse=true&threshold=1")
        shot(page, "proposals-collapsed")

        print("capturing a proposal still open for improvement")
        page.goto(f"{BASE}{opening}")
        shot(page, "proposal-editable")

        print("capturing a proposal that has locked and is being voted on")
        page.goto(f"{BASE}{instrumental}")
        shot(page, "proposal-locked")

        print("assembling alternative solutions")
        sign_in(page, "listener@example.com")
        assemble(page, songs,
                 "A short, focused record: open strong, keep it under 50 minutes, no filler.",
                 [0, 1, 2])

        sign_in(page, "editor@example.com")
        assemble(page, songs,
                 "A career retrospective instead — breadth over tightness, including rarities.",
                 [1, 3])

        # Editor backs their own retrospective; Chair prefers the short record.
        retrospective = alternative_url(page, songs, "career retrospective")
        focused = alternative_url(page, songs, "short, focused record")
        vote_on_group(page, retrospective, 1)

        sign_in(page, "chair@example.com")
        vote_on_group(page, focused, 1)
        comment_on_group(page, focused,
                         "The under-50-minutes rule is what makes this work — it forces the choices.")

        print("capturing the alternatives")
        page.goto(f"{songs}/alternatives")
        shot(page, "alternatives-list")

        page.goto(f"{BASE}{focused}")
        shot(page, "alternative-detail")

        print("evaluating the alternatives privately")
        # Chair cares most about running time; the focused record scores well on it, the
        # retrospective does not.
        evaluate(page, songs, focused, weights=[5, 2, 3], scores=[5, 4, 4])
        evaluate(page, songs, retrospective, weights=[5, 2, 3], scores=[1, 5, 3])

        print("capturing the evaluation form and comparison")
        page.goto(f"{songs}/evaluate/{focused.rsplit('/', 1)[-1]}")
        shot(page, "evaluate-form")
        page.goto(f"{songs}/evaluate")
        shot(page, "evaluate-compare")

        print("tagging proposals with marker words and searching them back")
        sign_in(page, "chair@example.com")
        comment_on_proposal(page, opening, "pros: it is the strongest opening we have")
        comment_on_proposal(page, instrumental, "cons: nobody agrees which instrumental")
        comment_on_proposal(page, closing, "cons: the longest track drags at the end")

        page.goto(f"{songs}/search?q=pros+OR+cons&mode=Proposals")
        shot(page, "search-tags")

        print("mapping how the factors pull against each other")
        factors = ["Journey time", "Air quality", "Shop takings", "Cost to the council"]
        table_url = build_factor_table(page, songs, "How the factors pull against each other", factors, {
            (0, 1): ("StronglyPositive", "Fewer cars idling"),
            (0, 2): ("Negative", "Harder to reach the shops"),
            (0, 3): ("Negative", "Enforcement is not free"),
            (1, 0): ("None", None),
            (1, 2): ("Positive", "A pleasanter street brings people back"),
            (2, 3): ("StronglyPositive", "Business rates go up"),
            (3, 0): ("Negative", "Budget pressure delays the works"),
        })
        page.goto(f"{BASE}{table_url.replace(BASE, '')}")
        page.click("form[action$='/share'] button")
        page.wait_for_load_state("networkidle")
        shot(page, "factor-table")

        print("capturing the privacy controls")
        page.goto(f"{BASE}/profiles/me")
        page.fill("#RealName", "A. Chairperson")
        page.fill("#Location", "Thessaloniki")
        page.fill("#Biography", "Interested in how large groups reach a decision.")
        page.select_option("#LocationVisibility", "Public")
        page.select_option("#BiographyVisibility", "Members")
        page.click("main form button[type=submit]")
        page.wait_for_load_state("networkidle")
        page.goto(f"{BASE}/profiles/me")
        shot(page, "profile-privacy")

        # Scoped to main: the nav's "My profile" link points at /profiles/me, which would
        # simply redirect an anonymous visitor to the sign-in page.
        chair_id = page.query_selector("main a[href*='/profiles/']").get_attribute("href")
        chair_uuid = chair_id.rsplit("/", 1)[-1]

        print("attaching a file to a proposal")
        attachment = pathlib.Path(os.environ.get("TEMP", ".")) / "session-players-1971.csv"
        attachment.write_text(
            "track,instrument,player\n"
            "3,flute,uncredited session player\n"
            "7,strings,city chamber ensemble\n",
            encoding="utf-8")
        page.goto(f"{BASE}{instrumental}")
        page.set_input_files("#attachments input[type=file]", str(attachment))
        page.click("#attachments button")
        page.wait_for_load_state("networkidle")
        page.goto(f"{BASE}{instrumental}")
        shot_region(page, "#attachments", "proposal-attachments")

        print("exchanging private messages")
        sign_in(page, "editor@example.com")
        page.goto(f"{BASE}/messages/{chair_uuid}")
        page.fill("textarea[name=body]",
                  "Would you take a look at the retrospective before the vote closes? "
                  "I think the rarities argument needs your view.")
        # The send button carries no explicit type, so match the form rather than the attribute.
        page.click("form[action*='/messages/'] button")
        page.wait_for_load_state("networkidle")

        sign_in(page, "chair@example.com")
        page.goto(f"{BASE}/messages")
        shot(page, "messages")

        # Captured unread, then opened — the badge in the shot above is the point of it.
        thread = page.query_selector("main a[href*='/messages/']").get_attribute("href")
        page.goto(f"{BASE}{thread}")
        page.fill("textarea[name=body]",
                  "Happy to. The breadth argument is the strongest part of it — say more about "
                  "which rarities you mean.")
        page.click("form[action*='/messages/'] button")
        page.wait_for_load_state("networkidle")
        shot(page, "conversation")

        print("capturing the notifications page")
        page.goto(f"{BASE}/notifications")
        shot(page, "notifications")

        print("capturing the anonymous view")
        sign_out(page)
        page.goto(f"{BASE}{chair_id}")
        shot(page, "profile-anonymous")

        page.goto(traffic)
        shot(page, "topic-counts-hidden")

        page.goto(f"{BASE}/Account/Register")
        shot(page, "register")

        browser.close()
        print("done")


if __name__ == "__main__":
    sys.exit(main())
