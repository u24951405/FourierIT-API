"""
Shows SendGrid's real SMTP replies for the API's email settings, without sending an email.

It connects, logs in with the key from User Secrets (falling back to appsettings.Development.json),
announces the sender (MAIL FROM) and then resets and disconnects. The key is never printed.

Run from the FourierIT folder:  python FourierIT-API/tools/smtp_check.py
"""
import json
import os
import smtplib
import ssl
from pathlib import Path

API_DIR = Path(__file__).resolve().parent.parent
SECRETS = Path(os.environ["APPDATA"]) / "Microsoft" / "UserSecrets" / "2a3aef93-13ef-43f3-9aba-d36b40143b40" / "secrets.json"


def load_settings() -> dict:
    settings = json.loads((API_DIR / "appsettings.Development.json").read_text(encoding="utf-8-sig"))["EmailSettings"]
    if SECRETS.exists():
        secrets = json.loads(SECRETS.read_text(encoding="utf-8-sig"))
        for key in ("Host", "Port", "Username", "Password", "FromAddress"):
            value = secrets.get(f"EmailSettings:{key}") or secrets.get("EmailSettings", {}).get(key)
            if value:
                settings[key] = value
    return settings


def main() -> None:
    s = load_settings()
    print(f"Host: {s['Host']}:{s['Port']} | username: {s['Username']} | sender: {s['FromAddress']}")

    with smtplib.SMTP(s["Host"], int(s["Port"]), timeout=20) as smtp:
        smtp.ehlo()
        smtp.starttls(context=ssl.create_default_context())
        smtp.ehlo()

        try:
            code, reply = smtp.login(s["Username"], s["Password"])
            print(f"LOGIN     -> {code} {reply.decode(errors='replace')}")
        except smtplib.SMTPAuthenticationError as e:
            print(f"LOGIN     -> FAILED {e.smtp_code} {e.smtp_error.decode(errors='replace')}")
            print("\nThe key is being rejected: check it exists, is copied fully, and has Mail Send permission.")
            return

        code, reply = smtp.mail(s["FromAddress"])
        print(f"MAIL FROM -> {code} {reply.decode(errors='replace')}")
        smtp.rset()  # stop here: nothing is sent

    print("\nDone. No email was sent.")


if __name__ == "__main__":
    try:
        main()
    except smtplib.SMTPServerDisconnected as e:
        print(f"SendGrid closed the connection: {e}")
    except Exception as e:  # show the reason plainly
        print(f"{type(e).__name__}: {e}")
