<#import "template.ftl" as layout>
<#--
	Shown when a login form has been sitting open long enough for its
	authentication session to lapse - a tab left open overnight, or the back
	button after signing in.

	Base renders the two ways forward as "Um den Anmeldevorgang neu zu starten
	<a>Hier klicken</a> ." - a sentence with the verb in the link text, a
	stray space before the full stop, and no visual difference between the
	recoverable option and the destructive one. They are two buttons here, in
	the order a person actually wants them: continue where you left off first,
	start over second.
-->
<@layout.registrationLayout
	pageTitle="pageExpiredTitle"
	eyebrow="stepSession"
	lead="pageExpiredLead"; section>

	<#if section = "header">
		${msg("pageExpiredTitle")}

	<#elseif section = "form">
		<div id="kc-page-expired" class="${properties.kcFormButtonsClass!}">
			<a
				id="loginContinueLink"
				class="${properties.kcButtonClass!} ${properties.kcButtonPrimaryClass!} ${properties.kcButtonBlockClass!} ${properties.kcButtonLargeClass!}"
				href="${url.loginAction}"
			>${msg("pageExpiredContinue")}</a>
			<a
				id="loginRestartLink"
				class="${properties.kcButtonClass!} ${properties.kcButtonDefaultClass!} ${properties.kcButtonBlockClass!} ${properties.kcButtonLargeClass!}"
				href="${url.loginRestartFlowUrl}"
			>${msg("pageExpiredRestart")}</a>
		</div>
	</#if>

</@layout.registrationLayout>
