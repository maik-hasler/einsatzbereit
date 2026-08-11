<#import "template.ftl" as layout>
<#--
	The most-visited page in this theme after sign-in itself, and the one that
	was worst off unstyled. Two ways in, both unavoidable: registration (with
	verifyEmail on, Keycloak leaves the password off the registration form and
	sets UPDATE_PASSWORD once the address is confirmed - see
	RegistrationPassword.buildPage - so this is where every new account
	actually gets its password), and the "forgot password" mail.

	Base's version puts the <label> in a wrapper div *above* the input rather
	than as its sibling, which this theme's floating-label rules cannot match -
	so the absolutely-positioned label detached from the field entirely. The
	markup below is the same field structure as login.ftl instead.
-->
<@layout.registrationLayout
	displayMessage=!messagesPerField.existsError('password','password-confirm')
	pageTitle="updatePasswordTitle"
	eyebrow="stepSecurity"
	lead="updatePasswordLead"; section>

	<#if section = "header">
		${msg("updatePasswordTitle")}

	<#elseif section = "form">
		<form id="kc-passwd-update-form" class="${properties.kcFormClass!}" action="${url.loginAction}" method="post">

			<div class="form-group">
				<div class="form-field form-field--with-toggle">
					<input
						type="password"
						id="password-new"
						name="password-new"
						class="${properties.kcInputClass!}"
						aria-invalid="<#if messagesPerField.existsError('password','password-confirm')>true</#if>"
						autocomplete="new-password"
						autofocus
						required
						placeholder=" "
					/>
					<label for="password-new" class="${properties.kcLabelClass!}">${msg("passwordNew")}</label>
					<#if properties.kcFormPasswordVisibilityButtonClass?has_content>
						<button
							class="${properties.kcFormPasswordVisibilityButtonClass!}"
							type="button"
							aria-label="${msg('showPassword')}"
							aria-controls="password-new"
							data-password-toggle
							data-icon-show="${properties.kcFormPasswordVisibilityIconShow!}"
							data-icon-hide="${properties.kcFormPasswordVisibilityIconHide!}"
							data-label-show="${msg('showPassword')}"
							data-label-hide="${msg('hidePassword')}"
						>
							<i class="${properties.kcFormPasswordVisibilityIconShow!}" aria-hidden="true"></i>
						</button>
					</#if>
				</div>
				<#if messagesPerField.existsError('password')>
					<span id="input-error-password" class="${properties.kcInputErrorMessageClass!}" aria-live="polite">
						${kcSanitize(messagesPerField.get('password'))?no_esc}
					</span>
				</#if>
			</div>

			<div class="form-group">
				<div class="form-field form-field--with-toggle">
					<input
						type="password"
						id="password-confirm"
						name="password-confirm"
						class="${properties.kcInputClass!}"
						aria-invalid="<#if messagesPerField.existsError('password-confirm')>true</#if>"
						autocomplete="new-password"
						required
						placeholder=" "
					/>
					<label for="password-confirm" class="${properties.kcLabelClass!}">${msg("passwordConfirm")}</label>
					<#if properties.kcFormPasswordVisibilityButtonClass?has_content>
						<button
							class="${properties.kcFormPasswordVisibilityButtonClass!}"
							type="button"
							aria-label="${msg('showPassword')}"
							aria-controls="password-confirm"
							data-password-toggle
							data-icon-show="${properties.kcFormPasswordVisibilityIconShow!}"
							data-icon-hide="${properties.kcFormPasswordVisibilityIconHide!}"
							data-label-show="${msg('showPassword')}"
							data-label-hide="${msg('hidePassword')}"
						>
							<i class="${properties.kcFormPasswordVisibilityIconShow!}" aria-hidden="true"></i>
						</button>
					</#if>
				</div>
				<#if messagesPerField.existsError('password-confirm')>
					<span id="input-error-password-confirm" class="${properties.kcInputErrorMessageClass!}" aria-live="polite">
						${kcSanitize(messagesPerField.get('password-confirm'))?no_esc}
					</span>
				</#if>
			</div>

			<div class="form-field form-field-checkbox">
				<input type="checkbox" id="logout-sessions" name="logout-sessions" value="on" class="checkbox-control" checked>
				<label for="logout-sessions">${msg("logoutOtherSessions")}</label>
			</div>

			<div id="kc-form-buttons" class="${properties.kcFormButtonsClass!}">
				<#-- "Save password", not Keycloak's generic "Submit": the label on
				the control should be what happens when you press it. -->
				<input
					name="login"
					class="${properties.kcButtonClass!} ${properties.kcButtonPrimaryClass!} ${properties.kcButtonBlockClass!} ${properties.kcButtonLargeClass!}"
					type="submit"
					value="${msg('doSetPassword')}"
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
