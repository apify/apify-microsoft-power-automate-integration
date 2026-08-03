#!/usr/bin/env python3
"""Validate Power Automate connector files against Microsoft certification policies.

Reference: https://learn.microsoft.com/en-us/connectors/custom-connectors/certification-policy-errors

Runs all checks and exits 1 if any failed. Does not fail-fast: surfaces every issue at once.
Requires Python 3.9+, no third-party dependencies.

Usage:
  python3 scripts/validate.py

Skips submission-package checks if ConnectorPackage/ConnectorPackage.zip is absent.
"""

from __future__ import annotations

import json
import re
import struct
import sys
import tempfile
import zipfile
from pathlib import Path
from typing import Any, Iterable

SWAGGER = Path("apiDefinition.swagger.json")
PROPS = Path("apiProperties.json")
ICON = Path("icon.png")
SCRIPT = Path("scripts.csx")
INTRO = Path("intro.md")
PKG_ZIP = Path("ConnectorPackage/ConnectorPackage.zip")

PASS = 0
FAIL = 0


def ok(code: str, msg: str) -> None:
    global PASS
    print(f"  [ pass ] {code:<12} {msg}")
    PASS += 1


def fail(code: str, msg: str) -> None:
    global FAIL
    print(f"  [ FAIL ] {code:<12} {msg}")
    FAIL += 1


def skip(code: str, msg: str) -> None:
    print(f"  [ skip ] {code:<12} {msg}")


def section(title: str) -> None:
    print()
    print(f"=== {title} ===")


def read_json(path: Path) -> Any:
    with open(path) as f:
        return json.load(f)


def walk_strings(node: Any, key_filter: str | None = None) -> Iterable[str]:
    """Yield every string value under `node`. If key_filter is set, only yield values
    whose direct parent key matches."""
    if isinstance(node, dict):
        for k, v in node.items():
            if isinstance(v, str) and (key_filter is None or k == key_filter):
                yield v
            else:
                yield from walk_strings(v, key_filter)
    elif isinstance(node, list):
        for v in node:
            yield from walk_strings(v, key_filter)


def png_info(path: Path) -> tuple[int, int, bool] | None:
    """Read PNG (width, height, has_alpha) from the IHDR chunk. None if not a PNG."""
    try:
        with open(path, "rb") as f:
            data = f.read(26)
    except OSError:
        return None
    if data[:8] != b"\x89PNG\r\n\x1a\n":
        return None
    width, height = struct.unpack(">II", data[16:24])
    color_type = data[25]
    # Color type 4 = grayscale + alpha, 6 = RGB + alpha. Type 3 (palette) can carry tRNS
    # transparency but that's rare and the visual review would catch it.
    has_alpha = color_type in (4, 6)
    return width, height, has_alpha


def main() -> int:
    # =============================================================
    section("File presence")
    all_present = True
    for f in (SWAGGER, PROPS, SCRIPT, ICON):
        if f.exists():
            ok("-", f"{f} present")
        else:
            fail("-", f"{f} missing")
            all_present = False

    if not all_present:
        print()
        print("Required source files missing, cannot continue.")
        return finish()

    try:
        swagger = read_json(SWAGGER)
        props = read_json(PROPS)
    except (json.JSONDecodeError, OSError) as e:
        print()
        print(f"Could not parse source JSON: {e}")
        return finish()

    # =============================================================
    section("Well-formedness")
    required_top = ["swagger", "info", "host", "basePath", "paths"]
    info = swagger.get("info") if isinstance(swagger, dict) else None
    missing_top = [k for k in required_top if k not in swagger]
    if not info or not info.get("title") or not info.get("version"):
        missing_top.append("info.title/version")
    if missing_top:
        fail("-", f"Swagger missing top-level fields: {missing_top}")
    else:
        ok("-", "Swagger has required top-level fields")

    if props.get("properties", {}).get("connectionParameters"):
        ok("-", "apiProperties has required fields")
    else:
        fail("-", "apiProperties missing properties.connectionParameters")

    if "public class Script : ScriptBase" in SCRIPT.read_text():
        ok("-", "scripts.csx contains Script : ScriptBase")
    else:
        fail("-", "scripts.csx missing 'public class Script : ScriptBase'")

    # 5000.1.1.16 pre-export sanity. If apiProperties.policyTemplateInstances is empty
    # here, the next solution export will produce a 2-byte policytemplateinstances.json
    # whose content disagrees with customizations.xml, and cert rejects the package.
    pti = props.get("properties", {}).get("policyTemplateInstances", [])
    if isinstance(pti, list) and len(pti) >= 1:
        ok("5000.1.1.16", f"apiProperties has {len(pti)} policy template instance(s) (export will be non-empty)")
    else:
        fail("5000.1.1.16",
             "apiProperties has no policyTemplateInstances. Exported policytemplateinstances.json "
             "will be empty and cert will fail with 'Invalid package. Solution not present at "
             "correct path or is invalid solution.'")

    # Connector metadata required by Partner Center listing.
    metadata = swagger.get("x-ms-connector-metadata", [])
    website_url = next((m.get("propertyValue", "") for m in metadata if m.get("propertyName") == "Website"), "")
    privacy_url = next((m.get("propertyValue", "") for m in metadata if m.get("propertyName") == "Privacy policy"), "")
    support_email = info.get("contact", {}).get("email", "")

    if website_url.startswith("https://"):
        ok("5000.3.1.3", f"Website URL set ({website_url})")
    else:
        fail("5000.3.1.3", "Website URL missing or not HTTPS in x-ms-connector-metadata")

    if privacy_url.startswith("https://"):
        ok("5000.3.1.5", f"Privacy policy URL set ({privacy_url})")
    else:
        fail("5000.3.1.5", "Privacy policy URL missing or not HTTPS in x-ms-connector-metadata")

    if re.match(r"^[^@\s]+@[^@\s]+\.[^@\s]+$", support_email):
        ok("5000.3.1.4", f"Support email set ({support_email})")
    else:
        fail("5000.3.1.4", "Support email missing or malformed in info.contact.email")

    # =============================================================
    section("Icon (5000.2.1.x)")
    icon = png_info(ICON)
    if icon is None:
        fail("5000.2.1.3", "Icon is not a valid PNG")
    else:
        ok("5000.2.1.3", "Icon is PNG")
        w, h, has_alpha = icon
        size = ICON.stat().st_size
        if size < 1_048_576:
            ok("5000.2.1.2", f"Icon {size} bytes (<1 MB)")
        else:
            fail("5000.2.1.2", f"Icon {size} bytes exceeds 1 MB")
        if w == h and 100 <= w <= 230:
            ok("5000.2.1.4", f"Icon {w}x{h} (square, 100-230 px)")
        else:
            fail("5000.2.1.4", f"Icon {w}x{h} not square or out of 100-230 range")
        if has_alpha:
            fail("5000.2.1.5", "Icon has alpha channel, background may be transparent")
        else:
            ok("5000.2.1.5", "Icon is opaque (no alpha channel)")

    brand = props.get("properties", {}).get("iconBrandColor", "")
    if not re.match(r"^#[0-9a-fA-F]{6}$", brand):
        fail("5000.2.1.1", f"iconBrandColor '{brand}' is not a valid 6-digit hex")
    elif brand.lower() in ("#ffffff", "#007ee5"):
        fail("5000.2.1.1", f"iconBrandColor '{brand}' is forbidden (white or default)")
    else:
        ok("5000.2.1.1", f"iconBrandColor '{brand}'")

    # =============================================================
    section("Title (5000.2.2.x)")
    title = info.get("title", "") if info else ""
    if 0 < len(title) <= 30:
        ok("5000.2.2.1", f"Title '{title}' is {len(title)} chars (≤30)")
    else:
        fail("5000.2.2.1", f"Title length {len(title)} not in 1-30")

    reserved = re.compile(r"\b(API|Connector)\b|Power Apps|Power Automate|Copilot|Microsoft|Power Platform",
                          re.IGNORECASE)
    if reserved.search(title):
        fail("5000.2.2.2", "Title contains reserved word")
    else:
        ok("5000.2.2.2", "Title has no reserved words")

    if title and title[-1].isalnum():
        ok("5000.2.2.3", "Title ends with alphanumeric")
    else:
        last = title[-1] if title else ""
        fail("5000.2.2.3", f"Title ends with non-alphanumeric: '{last}'")

    # =============================================================
    section("Description (5000.2.3.x)")
    desc = info.get("description", "") if info else ""
    if 30 <= len(desc) <= 500:
        ok("5000.2.3.4", f"Description is {len(desc)} chars (30-500)")
    else:
        fail("5000.2.3.4", f"Description length {len(desc)} not in 30-500")

    ms_names = re.compile(r"Power Apps|Power Automate|Copilot|Microsoft|Power Platform", re.IGNORECASE)
    if ms_names.search(desc):
        fail("5000.2.3.5", "Description contains Microsoft product name")
    else:
        ok("5000.2.3.5", "Description has no Microsoft product names")

    # Scan EVERY description (not just top-level) for URLs and non-ASCII content.
    all_descs = list(walk_strings(swagger, key_filter="description"))
    url_hits = [d for d in all_descs if re.search(r"https?://", d)]
    if not url_hits:
        ok("5000.2.3.6", "No URLs in any description")
    else:
        fail("5000.2.3.6", f"URL in description: {url_hits[0][:120]}")

    nonascii = [d for d in all_descs if any(ord(c) > 127 for c in d)]
    if not nonascii:
        ok("5000.2.3.6", "All descriptions are ASCII-only")
    else:
        fail("5000.2.3.6", f"Non-ASCII chars in description: {nonascii[0][:80]}")

    # =============================================================
    section("Operation responses (5000.2.4.x)")
    empty_ops: list[str] = []
    default_with_schema: list[str] = []
    for path, methods in swagger.get("paths", {}).items():
        if not isinstance(methods, dict):
            continue
        for verb, op in methods.items():
            if verb in ("parameters", "x-ms-notification-content") or not isinstance(op, dict):
                continue
            responses = op.get("responses", {})
            if not responses:
                empty_ops.append(f"{path} {verb}")
            for code, resp in responses.items():
                if code == "default" and isinstance(resp, dict) and "schema" in resp:
                    default_with_schema.append(op.get("operationId", "?"))

    if not empty_ops:
        ok("5000.2.4.5", "All operations define responses")
    else:
        fail("5000.2.4.5", f"Operations missing responses: {empty_ops}")
    if not default_with_schema:
        ok("5000.2.4.2", "No 'default' response defines a schema")
    else:
        fail("5000.2.4.2", f"Default response with schema on: {default_with_schema}")

    empty_schemas: list[str] = []

    def walk_empty(node: Any, p: str = "") -> None:
        if isinstance(node, dict):
            if node.get("type") == "object" and isinstance(node.get("properties"), dict) and not node["properties"]:
                empty_schemas.append(p or "<root>")
            for k, v in node.items():
                walk_empty(v, f"{p}.{k}")
        elif isinstance(node, list):
            for i, v in enumerate(node):
                walk_empty(v, f"{p}[{i}]")

    walk_empty(swagger)
    if not empty_schemas:
        ok("5000.2.4.4/6", "No empty object schemas")
    else:
        fail("5000.2.4.4/6", f"Empty schemas at: {empty_schemas}")

    # =============================================================
    section("Swagger structure (5000.2.6.x)")
    ver = swagger.get("swagger", "")
    if ver == "2.0":
        ok("5000.2.6.3", "swagger version is 2.0")
    else:
        fail("5000.2.6.3", f"swagger version is '{ver}' (expected 2.0)")

    raw = SWAGGER.read_text()
    if re.search(r'"(openapi|nullable|oneOf|anyOf|requestBody|components|servers)"', raw):
        fail("5000.2.6.3", "OpenAPI 3.0 keywords present")
    else:
        ok("5000.2.6.3", "No OpenAPI 3.0 keywords")

    conn_params = props.get("properties", {}).get("connectionParameters", {})
    empty_ui = [k for k, v in conn_params.items() if not v.get("uiDefinition")]
    if not empty_ui:
        ok("5000.2.6.9", "uiDefinition non-empty on all connection parameters")
    else:
        fail("5000.2.6.9", f"Empty uiDefinition on: {empty_ui}")

    empty_desc = [k for k, v in conn_params.items() if not v.get("uiDefinition", {}).get("description")]
    if not empty_desc:
        ok("5000.2.6.10", "uiDefinition.description set on all connection parameters")
    else:
        fail("5000.2.6.10", f"Empty uiDefinition.description on: {empty_desc}")

    # Connection-parameter UI constraints must use STRING booleans ("true"/"false"), not JSON
    # booleans. Microsoft certification rejected the connector specifically because
    # uiDefinition.constraints.hidden was the JSON boolean `false` instead of the string "false"
    # (MS feedback: 'change the hidden property to "false" rather than false'). None of the
    # structural validators (paconn, ConnectorPackageValidator.ps1) catch this, and the cert
    # report only shows a generic policy code, so enforce it locally.
    bad_constraints: list[str] = []
    for pname, pval in conn_params.items():
        constraints = pval.get("uiDefinition", {}).get("constraints", {}) or {}
        for ckey in ("required", "hidden"):
            if ckey in constraints and isinstance(constraints[ckey], bool):
                bad_constraints.append(f'{pname}.constraints.{ckey} = {str(constraints[ckey]).lower()}')
    if not bad_constraints:
        ok("constraints", 'uiDefinition.constraints use string booleans ("true"/"false")')
    else:
        fail("constraints",
             "uiDefinition.constraints must use STRING booleans, not JSON booleans "
             f'(offending: {bad_constraints}). Change to quoted strings, e.g. "hidden": "false" '
             'not "hidden": false. Microsoft certification rejects the boolean form.')

    # =============================================================
    section("Network security (5000.3.1.6)")
    http_hits: list[str] = []

    def walk_http(node: Any) -> None:
        if isinstance(node, dict):
            for v in node.values():
                walk_http(v)
        elif isinstance(node, list):
            for v in node:
                walk_http(v)
        elif isinstance(node, str) and node.startswith("http://"):
            http_hits.append(node)

    walk_http(swagger)
    walk_http(props)
    if not http_hits:
        ok("5000.3.1.6", "All URLs use HTTPS")
    else:
        fail("5000.3.1.6", f"Plain http:// URLs: {http_hits[:3]}")

    # =============================================================
    section("Repo hygiene (OAuth must not be a real value in source control)")
    # OAuth client ID/secret in apiProperties.json should be placeholder text or empty.
    # Real values are entered in Partner Center, not committed.
    token = conn_params.get("token", {}).get("oAuthSettings", {})
    client_id = token.get("clientId", "")
    client_secret = token.get("clientSecret", "")
    placeholder_re = re.compile(r"^\s*\{\{.*\}\}\s*$|^(REPLACE_ME|YOUR_.*|<.*>)$")

    def is_placeholder(v: str) -> bool:
        return not v or bool(placeholder_re.match(v))

    if is_placeholder(client_id):
        ok("secret", "OAuth clientId is placeholder or empty")
    else:
        fail("secret",
             "OAuth clientId looks like a real value. Use a {{ placeholder }} in the repo "
             "and set the real value in Partner Center.")

    if is_placeholder(client_secret):
        ok("secret", "OAuth clientSecret is placeholder or empty")
    else:
        fail("secret",
             "OAuth clientSecret looks like a real value. Use a {{ placeholder }} in the repo "
             "and set the real value in Partner Center.")

    # =============================================================
    section("Submission package (5000.1.1.x)")
    if not PKG_ZIP.exists():
        skip("5000.1.1.x", f"No {PKG_ZIP} yet. Build it before submission (see CONTRIBUTING.md).")
    else:
        check_submission_package(swagger, props)

    return finish()


def check_submission_package(swagger: dict, props: dict) -> None:
    with tempfile.TemporaryDirectory(prefix="apify-validate-") as tmp:
        tmpdir = Path(tmp)
        outer = tmpdir / "outer"
        try:
            with zipfile.ZipFile(PKG_ZIP) as z:
                z.extractall(outer)
        except (zipfile.BadZipFile, OSError) as e:
            fail("5000.1.1.x", f"Could not extract {PKG_ZIP}: {e}")
            return

        # Outer zip: exactly intro.md + package.zip at root, no wrapper folders.
        outer_entries = list(outer.iterdir())
        outer_dirs = [p.name for p in outer_entries if p.is_dir()]
        outer_files = sorted(p.name for p in outer_entries if p.is_file())

        if outer_dirs:
            fail("5000.1.1.11", f"Outer zip contains wrapper folder(s): {outer_dirs}")
        elif outer_files == ["intro.md", "package.zip"]:
            ok("5000.1.1.11", "Outer zip root: intro.md + package.zip only")
        else:
            fail("5000.1.1.11",
                 f"Outer zip root has unexpected files: {outer_files} "
                 "(expected ['intro.md', 'package.zip'])")

        # intro.md inside zip should match local intro.md (catches stale rebuilds).
        outer_intro = outer / "intro.md"
        if outer_intro.exists() and INTRO.exists():
            if outer_intro.read_bytes() == INTRO.read_bytes():
                ok("intro", "intro.md inside zip matches local intro.md")
            else:
                fail("intro", "intro.md inside zip differs from local intro.md (rebuild ConnectorPackage.zip)")

        # Extract inner package.zip.
        inner_zip = outer / "package.zip"
        if not inner_zip.exists():
            fail("5000.1.1.12", "Outer zip missing package.zip")
            return

        inner = tmpdir / "inner"
        try:
            with zipfile.ZipFile(inner_zip) as z:
                z.extractall(inner)
        except (zipfile.BadZipFile, OSError) as e:
            fail("5000.1.1.12", f"Could not extract package.zip: {e}")
            return

        pkg_assets = inner / "PkgAssets"
        if pkg_assets.is_dir():
            ok("5000.1.1.12", "package.zip contains PkgAssets/ at root")
        else:
            fail("5000.1.1.12", "package.zip missing PkgAssets/ folder at root")
            return

        pkg_files = sorted(p.name for p in pkg_assets.iterdir() if p.is_file())
        if pkg_files == ["ConnectorSolution.zip", "FlowSolution.zip"]:
            ok("5000.1.1.13", "PkgAssets has exactly ConnectorSolution.zip + FlowSolution.zip")
        else:
            fail("5000.1.1.13",
                 f"PkgAssets contents {pkg_files} (expected ['ConnectorSolution.zip', 'FlowSolution.zip'])")

        # Inspect each solution zip.
        solutions: dict[str, Path] = {}
        for sol_name in ("ConnectorSolution", "FlowSolution"):
            sol_zip = pkg_assets / f"{sol_name}.zip"
            if not sol_zip.exists():
                fail("5000.1.1.13", f"{sol_name}.zip not found in PkgAssets")
                continue
            sol_dir = tmpdir / f"sol_{sol_name}"
            try:
                with zipfile.ZipFile(sol_zip) as z:
                    z.extractall(sol_dir)
            except (zipfile.BadZipFile, OSError) as e:
                fail("5000.1.1.13", f"Could not extract {sol_name}.zip: {e}")
                continue
            solutions[sol_name] = sol_dir
            check_solution(sol_name, sol_dir)

        # FlowSolution-specific: Workflows folder + manifest match.
        if "FlowSolution" in solutions:
            check_flow_solution(solutions["FlowSolution"])

        # Cross-check: exported openapi inside each solution must match the local source.
        # If it doesn't, the developer edited source but forgot to re-push + re-export.
        src_title = swagger.get("info", {}).get("title", "")
        src_host = swagger.get("host", "")
        src_paths = len(swagger.get("paths", {}))
        for sol_name, sol_dir in solutions.items():
            exp_files = list((sol_dir / "Connector").glob("*_openapidefinition.json"))
            if not exp_files:
                continue
            try:
                exp = read_json(exp_files[0])
            except (json.JSONDecodeError, OSError) as e:
                fail("sync", f"{sol_name}: could not read exported openapi ({e})")
                continue
            exp_title = exp.get("info", {}).get("title", "")
            exp_host = exp.get("host", "")
            exp_paths = len(exp.get("paths", {}))
            if (src_title, src_host, src_paths) == (exp_title, exp_host, exp_paths):
                ok("sync",
                   f"{sol_name} openapi matches local source "
                   f"(title='{exp_title}' host='{exp_host}' paths={exp_paths})")
            else:
                fail("sync",
                     f"{sol_name} openapi is stale. "
                     f"Local: title='{src_title}' host='{src_host}' paths={src_paths}. "
                     f"Exported: title='{exp_title}' host='{exp_host}' paths={exp_paths}. "
                     "Re-push with paconn update and re-export.")

        # Cross-check: exported policytemplateinstances must match local source.
        src_pti = props.get("properties", {}).get("policyTemplateInstances", [])
        for sol_name, sol_dir in solutions.items():
            exp_files = list((sol_dir / "Connector").glob("*_policytemplateinstances.json"))
            if not exp_files:
                continue
            try:
                exp_pti = read_json(exp_files[0])
            except (json.JSONDecodeError, OSError) as e:
                fail("sync", f"{sol_name}: could not read exported policytemplateinstances ({e})")
                continue
            if src_pti == exp_pti:
                ok("sync", f"{sol_name} policytemplateinstances matches local apiProperties")
            else:
                fail("sync",
                     f"{sol_name} policytemplateinstances is stale vs local apiProperties. "
                     "Re-push and re-export so the manifest matches.")


def check_solution(name: str, sol_dir: Path) -> None:
    required_root = ["[Content_Types].xml", "solution.xml", "customizations.xml"]
    missing = [r for r in required_root if not (sol_dir / r).exists()]
    if not missing:
        ok("5000.1.1.10",
           f"{name} has all root files (solution.xml, customizations.xml, [Content_Types].xml)")
    else:
        fail("5000.1.1.10", f"{name} missing root files: {missing}")

    # Must be Unmanaged for cert submission. Managed=1 is for production deploys.
    sol_xml = sol_dir / "solution.xml"
    if sol_xml.exists():
        text = sol_xml.read_text()
        m = re.search(r"<Managed>([01])</Managed>", text)
        if m and m.group(1) == "0":
            ok("managed", f"{name} is Unmanaged")
        else:
            val = m.group(0) if m else "(not found)"
            fail("managed",
                 f"{name} is not Unmanaged ({val}). Cert team rejects Managed exports. "
                 "Re-export with 'Unmanaged' selected.")

        # Publisher-prefix ownership (HEURISTIC, not a documented policy). Microsoft's
        # certification docs prescribe no rule about the connector logical/schema name or
        # publisher prefix, so this is an advisory only: a default "new_" prefix means the
        # connector was authored under the default Dataverse publisher rather than the Apify
        # publisher. Re-authoring under the apify publisher is recommended for clean ALM and
        # is the same re-author that fixes the documented duplication issue below, but a
        # "new_" prefix is NOT a confirmed cert blocker. Reported as a warning, never fails.
        prefix_m = re.search(r"<CustomizationPrefix>([^<]+)</CustomizationPrefix>", text)
        prefix = prefix_m.group(1).strip() if prefix_m else ""
        cust_path = sol_dir / "customizations.xml"
        conn_name = ""
        if cust_path.exists():
            nm = re.search(r"<Connector>.*?<name>([^<]+)</name>", cust_path.read_text(), re.S)
            conn_name = nm.group(1).strip() if nm else ""
        if conn_name and prefix:
            if conn_name.startswith(prefix + "_"):
                ok("prefix", f"{name} connector '{conn_name}' uses publisher prefix '{prefix}_'")
            else:
                skip("prefix",
                     f"{name} connector '{conn_name}' uses the default 'new_' prefix, not the "
                     f"'{prefix}' publisher prefix. Advisory only (no documented cert rule): "
                     f"consider re-authoring under the '{prefix}' publisher for clean ALM.")

        # Connector component presence (5000.1.1.8). RootComponent type 372 is the custom
        # connector. Per Microsoft's documented package structure, BOTH solutions legitimately
        # carry a Connector folder: ConnectorSolution holds the connector, and FlowSolution
        # also includes it as a dependency of the sample flows. So presence in both is correct,
        # not a duplication error. We only assert the connector is present where expected.
        has_connector = bool(re.search(r'<RootComponent\s+type="372"', text))
        if has_connector:
            ok("5000.1.1.8", f"{name} contains the connector component (type 372)")
        elif name == "ConnectorSolution":
            fail("5000.1.1.8", "ConnectorSolution is missing the connector component (type 372)")

    conn_dir = sol_dir / "Connector"
    if not conn_dir.is_dir():
        fail("5000.1.1.17", f"{name} missing Connector/ folder")
        return

    needed = [
        "*_openapidefinition.json",
        "*_connectionparameters.json",
        "*_policytemplateinstances.json",
        "*_customcodeblobcontent.csx",
        "*_iconblob.Png",
    ]
    for pat in needed:
        if list(conn_dir.glob(pat)):
            ok("5000.1.1.17", f"{name} has {pat}")
        else:
            fail("5000.1.1.17", f"{name} missing {pat} in Connector/")

    # The 5000.1.1.16 trip-wire: policytemplateinstances.json must not be the empty array.
    pti_files = list(conn_dir.glob("*_policytemplateinstances.json"))
    if pti_files:
        pti_size = pti_files[0].stat().st_size
        # Empty array '[]' is 2 bytes, with newline 3, with CRLF 4. Anything <= 4 is empty.
        if pti_size <= 4:
            fail("5000.1.1.16",
                 f"{name} policytemplateinstances.json is empty ({pti_size} bytes). "
                 "Populate apiProperties.policyTemplateInstances, re-push with paconn update, "
                 "re-export.")
        else:
            ok("5000.1.1.16", f"{name} policytemplateinstances.json is non-empty ({pti_size} bytes)")

        cust_xml = sol_dir / "customizations.xml"
        if cust_xml.exists():
            if "<policytemplateinstances>" in cust_xml.read_text():
                ok("5000.1.1.16", f"{name} customizations.xml references policytemplateinstances")
            else:
                fail("5000.1.1.16",
                     f"{name} has policytemplateinstances.json but customizations.xml does "
                     "not reference it (manifest/content mismatch)")


def check_flow_solution(sol_dir: Path) -> None:
    wf_dir = sol_dir / "Workflows"
    if not wf_dir.is_dir():
        fail("5000.1.1.14", "FlowSolution missing Workflows/ folder")
        return

    wf_files = list(wf_dir.glob("*.json"))
    if wf_files:
        ok("5000.1.1.14", f"FlowSolution has {len(wf_files)} workflow file(s) in Workflows/")
    else:
        fail("5000.1.1.14", "FlowSolution Workflows/ folder is empty")

    # Every <JsonFileName> in customizations.xml must exist on disk.
    cust_xml = sol_dir / "customizations.xml"
    if cust_xml.exists():
        referenced = re.findall(r"<JsonFileName>([^<]+)</JsonFileName>", cust_xml.read_text())
        missing = [r for r in referenced if not (sol_dir / r.lstrip("/")).exists()]
        if not missing:
            ok("5000.1.1.15",
               "FlowSolution: every <JsonFileName> in customizations.xml exists on disk")
        else:
            fail("5000.1.1.15", f"FlowSolution: missing workflow JSON file(s): {missing}")


def finish() -> int:
    total = PASS + FAIL
    print()
    if FAIL > 0:
        print(f"✗ {FAIL} of {total} checks failed")
        return 1
    print(f"✓ All {total} checks passed")
    return 0


if __name__ == "__main__":
    sys.exit(main())
