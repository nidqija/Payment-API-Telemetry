import os
import re
import subprocess
import sys

def main():
    changelog_path = 'CHANGELOG.html'
    if not os.path.exists(changelog_path):
        print(f"Error: {changelog_path} not found.", file=sys.stderr)
        sys.exit(1)

    with open(changelog_path, 'r', encoding='utf-8') as f:
        html = f.read()

    # Locate the latest release section in CHANGELOG.html
    sec_match = re.search(r'<section class="release-section"[^>]*>(.*?)</section>', html, re.DOTALL)
    if not sec_match:
        print("Error: No release-section found in CHANGELOG.html", file=sys.stderr)
        sys.exit(1)

    sec = sec_match.group(1)

    # Extract version tag (e.g., v1.3.0)
    tag_match = re.search(r'class="version-tag">\s*([^<]+)', sec)
    tag = tag_match.group(1).strip() if tag_match else "v1.0.0"

    # Extract summary heading & release date
    summary_match = re.search(r'class="release-summary">\s*([^<]+)', sec)
    summary = summary_match.group(1).strip() if summary_match else "Dev Release"

    date_match = re.search(r'class="release-date">\s*([^<]+)', sec)
    rel_date = date_match.group(1).strip() if date_match else "Latest"

    # Extract categories and individual feature notes
    features_text = []
    for cat in re.finditer(r'<div class="category-heading[^"]*">\s*<span>([^<]*)</span>\s*([^<]+)</div>\s*<ul class="feature-items-list">(.*?)</ul>', sec, re.DOTALL):
        icon = cat.group(1).strip()
        cat_name = cat.group(2).strip()
        features_text.append(f"#### {icon} {cat_name}\n")
        
        items_html = cat.group(3)
        for item in re.finditer(r'<div class="feature-title">\s*([^<]+)</div>\s*<div class="feature-desc">\s*(.*?)\s*</div>', items_html, re.DOTALL):
            title = item.group(1).strip()
            raw_desc = item.group(2).strip()
            # Clean up HTML formatting tags inside descriptions
            desc = re.sub(r'<code>([^<]+)</code>', r'`\1`', raw_desc)
            desc = re.sub(r'<[^>]+>', '', desc)
            desc = re.sub(r'\s+', ' ', desc)
            features_text.append(f"- **{title}**\n  {desc}\n")

    # Extract commit list
    commits_text = []
    for commit in re.finditer(r'<span class="commit-msg">\s*([^<]+)</span>\s*<span class="commit-hash"[^>]*>\s*([^<]+)</span>', sec):
        c_msg = commit.group(1).strip()
        c_hash = commit.group(2).strip()
        commits_text.append(f"- `{c_hash}` - {c_msg}")

    # Get short commit SHA
    try:
        short_sha = subprocess.check_output(['git', 'rev-parse', '--short', 'HEAD']).decode('utf-8').strip()
    except Exception:
        short_sha = "latest"

    # Build formatted markdown developer notes
    features_body = "\n".join(features_text)
    commits_body = "\n".join(commits_text) if commits_text else "- *(No commit hashes listed)*"

    notes = f"""# 🚀 PaymentGateway.Api - {tag}

> **{summary}**  
> *Released: {rel_date}* • *Trigger: Merged to `master`* • *Commit: `{short_sha}`*

---

### 📋 Developer Release Notes & Highlights

{features_body}

---

### 🔨 Commits in this Release
{commits_body}

---

### 📦 Artifacts
- **Binary Package**: `paymentgateway-api-{tag}.zip`
- **Interactive Changelog**: `CHANGELOG.html`
- **Docker Tags**: `paymentgateway-api:dev`, `paymentgateway-api:{tag}`
"""

    with open('RELEASE_NOTES.md', 'w', encoding='utf-8') as out:
        out.write(notes)

    # Pass metadata outputs to GitHub Actions if GITHUB_OUTPUT is defined
    gh_output = os.environ.get('GITHUB_OUTPUT')
    if gh_output:
        with open(gh_output, 'a', encoding='utf-8') as out:
            out.write(f"version={tag}\n")
            out.write(f"tag_name=developer-update-{tag}\n")
            out.write(f"summary={summary}\n")
            out.write(f"date={rel_date}\n")
            out.write(f"short_sha={short_sha}\n")

    print(f"Generated RELEASE_NOTES.md for developer-update-{tag}: {summary}")

if __name__ == '__main__':
    main()
