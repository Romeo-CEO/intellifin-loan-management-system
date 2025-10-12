<!DOCTYPE html>
<html lang="en">
  <head>
    <meta charset="utf-8" />
    <title>Reset Your IntelliFin Password</title>
  </head>
  <body style="margin:0;padding:0;background-color:#F8FAFC;font-family:Inter,'Segoe UI',Roboto,sans-serif;">
    <table width="100%" cellpadding="0" cellspacing="0" role="presentation" style="background-color:#F8FAFC;padding:24px 0;">
      <tr>
        <td align="center">
          <table width="640" cellpadding="0" cellspacing="0" role="presentation" style="background-color:#FFFFFF;border-radius:16px;border:1px solid #E5E7EB;box-shadow:0 16px 32px rgba(13,27,42,0.08);overflow:hidden;">
            <tr>
              <td style="background:linear-gradient(135deg,#0B6B62 0%,#14B8A6 100%);padding:32px 40px;color:#FFFFFF;">
                <h1 style="margin:0;font-size:26px;font-weight:700;letter-spacing:-0.3px;">Password Reset</h1>
                <p style="margin:12px 0 0;font-size:15px;opacity:0.88;">You requested to reset the password for your IntelliFin Loan Management System account.</p>
              </td>
            </tr>
            <tr>
              <td style="padding:40px;">
                <p style="margin:0 0 16px;font-size:16px;color:#0D1B2A;">Hello ${user.firstName?default("IntelliFin User")},</p>
                <p style="margin:0 0 24px;font-size:15px;color:#334155;line-height:1.6;">
                  To choose a new password, click the secure button below. The link will expire in ${linkExpiration} minutes to protect your account.
                </p>
                <p style="margin:0 0 32px;">
                  <a href="${link}" style="display:inline-block;background-color:#0B6B62;color:#FFFFFF;text-decoration:none;padding:14px 32px;border-radius:12px;font-weight:600;font-size:16px;">Create New Password</a>
                </p>
                <p style="margin:0 0 16px;font-size:14px;color:#6B7280;">
                  If you did not request this change, please contact the IntelliFin security team immediately at
                  <a href="mailto:security@intellifin.local" style="color:#2563EB;text-decoration:none;">security@intellifin.local</a>.
                </p>
                <p style="margin:0;font-size:13px;color:#94A3B8;">For your security, this link can be used only once.</p>
              </td>
            </tr>
            <tr>
              <td style="background-color:#F1F5F9;padding:24px 40px;text-align:center;color:#64748B;font-size:12px;">
                &copy; ${.now?string["yyyy"]} IntelliFin Holdings. All rights reserved. <br />
                Plot 4015, Great East Road, Lusaka, Zambia
              </td>
            </tr>
          </table>
        </td>
      </tr>
    </table>
  </body>
</html>
