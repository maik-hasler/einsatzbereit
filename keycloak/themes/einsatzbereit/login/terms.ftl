<#import "template.ftl" as layout>
<#--
	The TERMS_AND_CONDITIONS *required action*, as opposed to the registration
	authenticator of the same name that this realm already runs (see
	register.ftl's consent checkbox). It is one realm setting away rather than
	reachable today, but the theme owns the terms copy either way, and an
	unstyled page here would land on existing accounts - the people least
	expecting one.

	The two buttons carry the same weight in base, side by side. Accepting is
	the action the page is asking for; declining ends the session.
-->
<@layout.registrationLayout
	displayMessage=false
	pageTitle="termsTitle"
	eyebrow="stepTerms"
	lead="termsIntro"; section>

	<#if section = "header">
		${msg("termsTitle")}

	<#elseif section = "form">
		<div id="kc-terms-text">
			${kcSanitize(msg("termsDetails"))?no_esc}
		</div>

		<form class="${properties.kcFormClass!}" action="${url.loginAction}" method="POST">
			<div id="kc-form-buttons" class="${properties.kcFormButtonsClass!}">
				<input
					class="${properties.kcButtonClass!} ${properties.kcButtonPrimaryClass!} ${properties.kcButtonBlockClass!} ${properties.kcButtonLargeClass!}"
					name="accept"
					id="kc-accept"
					type="submit"
					value="${msg('doAccept')}"
				/>
				<input
					class="${properties.kcButtonClass!} ${properties.kcButtonDefaultClass!} ${properties.kcButtonBlockClass!} ${properties.kcButtonLargeClass!}"
					name="cancel"
					id="kc-decline"
					type="submit"
					value="${msg('doDecline')}"
				/>
			</div>
		</form>
	</#if>

</@layout.registrationLayout>
