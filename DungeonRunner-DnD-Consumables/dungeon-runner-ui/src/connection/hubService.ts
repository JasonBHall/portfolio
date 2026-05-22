import * as signalR from '@microsoft/signalr'

// Always connect to /gamehub on whatever host served this page.
// Vite's proxy then forwards it to the backend on localhost:5000.
// This means the same build works via localhost, LAN IP, or WAN.
const HUB_URL = `${window.location.protocol}//${window.location.host}/gamehub`

export const connection = new signalR.HubConnectionBuilder()
  .withUrl(HUB_URL)
  .withAutomaticReconnect()
  .configureLogging(signalR.LogLevel.Warning)
  .build()

export async function startConnection(): Promise<void> {
  if (connection.state === signalR.HubConnectionState.Disconnected) {
    await connection.start()
    console.log('[Hub] Connected to', HUB_URL)
  }
}
