<#import "template.ftl" as layout>
<@layout.emailLayout>
  <div style="font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px;">
    <div style="background-color: #0066cc; padding: 20px; text-align: center;">
      <img src="${logoUrl}" alt="IntelliFin" style="max-width: 200px;" />
    </div>
    
    <div style="padding: 30px; background-color: #f9f9f9;">
      <h2>Password Reset Request</h2>
      
      <p>Hello ${user.firstName},</p>
      
      <p>We received a request to reset your password for your IntelliFin account. If you made this request, click the button below to reset your password:</p>
      
      <div style="text-align: center; margin: 30px 0;">
        <a href="${link}" style="background-color: #0066cc; color: white; padding: 12px 30px; text-decoration: none; border-radius: 4px; font-weight: bold; display: inline-block;">Reset Password</a>
      </div>
      
      <p>This link will expire in <strong>24 hours</strong>.</p>
      
      <p>If you didn't request a password reset, please ignore this email or contact support if you have concerns.</p>
      
      <div style="border-top: 1px solid #ddd; margin-top: 30px; padding-top: 20px; font-size: 12px; color: #666;">
        <p>For security reasons, please do not share this email or the reset link with anyone.</p>
        <p>If the button above doesn't work, copy and paste this URL into your browser:<br/>
        <a href="${link}" style="word-break: break-all;">${link}</a></p>
      </div>
    </div>
    
    <div style="text-align: center; padding: 20px; color: #666; font-size: 12px;">
      <p>&copy; ${.now?string('yyyy')} IntelliFin. All rights reserved.</p>
      <p>Questions? Contact us at support@intellifin.local</p>
    </div>
  </div>
</@layout.emailLayout>
