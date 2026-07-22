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


def reset_database():
    """Start from an empty database so the captures are reproducible."""
    import pymysql
    con = pymysql.connect(host=DB_HOST, port=3306, user=DB_USER, password=DB_PASSWORD,
                          ssl={"ssl_mode": "REQUIRED"}, database=DB_NAME, connect_timeout=15)
    cur = con.cursor()
    cur.execute("SET FOREIGN_KEY_CHECKS = 0")
    for t in ("Comments", "Requirements", "Votes", "TopicMembers", "Topics", "UserProfiles",
              "AspNetUserClaims", "AspNetUserLogins", "AspNetUserRoles", "AspNetUserTokens",
              "AspNetUsers"):
        cur.execute(f"TRUNCATE TABLE `{t}`")
    cur.execute("SET FOREIGN_KEY_CHECKS = 1")
    con.commit()
    con.close()
    print("database cleared")


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

        print("capturing the proposal pool")
        sign_in(page, "chair@example.com")
        page.goto(f"{songs}/proposals")
        shot(page, "proposals-list")

        print("capturing a proposal still open for improvement")
        page.goto(f"{BASE}{opening}")
        shot(page, "proposal-editable")

        print("capturing a proposal that has locked and is being voted on")
        page.goto(f"{BASE}{instrumental}")
        shot(page, "proposal-locked")

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
