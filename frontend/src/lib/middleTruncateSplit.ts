export function splitForMiddleTruncation(text: string): [string, string] {
	const headLength = Math.ceil(text.length / 2);
	return [text.slice(0, headLength), text.slice(headLength)];
}
