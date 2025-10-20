<#import "template.ftl" as layout>
<@layout.emailLayout>
  <div style="font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px;">
    <div style="background-color: #0066cc; padding: 20px; text-align: center;">
      <img src="${logoUrl}" alt="IntelliFin" style="max-width: 200px;" />
    </div>
    
    <div style="padding: 30px; background-color: #f9f9f9;">
      <h2>Welcome to IntelliFin!</h2>
      
      <p>Hello ${user.firstName},</p>
      
      <p>Your IntelliFin account has been created. To complete your registration and activate your account, please verify your email address by clicking the button below:</p>
      
      <div style="text-align: center; margin: 30px 0;">
        <a href="${link}" style="background-color: #28a745; color: white; padding: 12px 30px; text-decoration: none; border-radius: 4px; font-weight: bold; display: inline-block;">Verify Email</a>
      </div>
      
      <p>This link will expire in <strong>72 hours</strong>.</p>
      
      <p>Once verified, you'll be able to access all features of your IntelliFin account.</p>
      
      <div style="border-top: 1px solid #ddd; margin-top: 30px; padding-top: 20px; font-size: 12px; color: #666;">
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
