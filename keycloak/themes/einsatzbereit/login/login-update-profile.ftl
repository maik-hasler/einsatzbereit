<#import "template.ftl" as layout>
<#import "user-profile-commons.ftl" as userProfileCommons>
<#--
	Shown when the realm needs a profile attribute this account does not have
	yet - an admin-set UPDATE_PROFILE required action, or a user-profile
	attribute that became required after the account was created.

	The fields come from base's userProfileCommons macro rather than a
	hand-written list, because the set of attributes is realm configuration:
	hardcoding username/email/firstName/lastName here would silently drop any
	attribute added later. That macro renders its labels above the inputs
	instead of as their siblings, so these fields deliberately fall to the
	static-label treatment in einsatzbereit.css rather than the floating labels
	used on the fixed forms - a label that cannot float should look like a
	plain label, not like one stuck mid-field.
-->
<@layout.registrationLayout
	displayMessage=messagesPerField.exists('global')
	displayRequiredFields=true
	pageTitle="loginProfileTitle"
	eyebrow="stepProfile"
	lead="loginProfileLead"; section>

	<#if section = "header">
		${msg("loginProfileTitle")}

	<#elseif section = "form">
		<form id="kc-update-profile-form" class="${properties.kcFormClass!}" action="${url.loginAction}" method="post">

			<@userProfileCommons.userProfileFormFields/>

			<div id="kc-form-buttons" class="${properties.kcFormButtonsClass!}">
				<input
					class="${properties.kcButtonClass!} ${properties.kcButtonPrimaryClass!} ${properties.kcButtonBlockClass!} ${properties.kcButtonLargeClass!}"
					type="submit"
					value="${msg('doSaveProfile')}"
				/>
				<#if isAppInitiatedAction??>
					<button
						class="${properties.kcButtonClass!} ${properties.kcButtonDefaultClass!} ${properties.kcButtonBlockClass!} ${properties.kcButtonLargeClass!}"
						type="submit"
						name="cancel-aia"
						value="true"
						formnovalidate
					>${msg("doCancel")}</button>
				</#if>
			</div>

		</form>
	</#if>

</@layout.registrationLayout>
