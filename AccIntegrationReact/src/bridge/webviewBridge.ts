import { useEffect, useRef } from "react";
import type { IncomingMessage, OutgoingMessage } from "./types";

/**
 * Send message to C# host. Falls back into console when running in browser
 */

export function sendToHost(message: OutgoingMessage) {
    if (window.chrome?.webview) {
        window.chrome.webview.postMessage(JSON.stringify(message))
    } else {
        console.log('sendTohost:' + message)
    }
}

/**
 * Subscribe to host messages/event
 */
export function useHostMessage(onMessage: (message: IncomingMessage) => void) {
    //useRef to subscribe to messages only one, instead every time the app render
    const handleRef = useRef(onMessage)
    handleRef.current = onMessage;

    useEffect(() => {
        if (!window.chrome?.webview) return

        const listener = (event: MessageEvent<string>) => {
            try {
                const data: IncomingMessage = JSON.parse(event.data);
                handleRef.current(data);
            } catch (err) {
                console.error('Failed to parse messge from useHostMessage', event.data, err)
            }
        };
        window.chrome.webview.addEventListener('message', listener)
        return () => window.chrome.webview.removeEventListener('message', listener)
    }, [])
}