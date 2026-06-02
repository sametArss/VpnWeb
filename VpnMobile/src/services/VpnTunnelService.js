import WireGuardVPN from 'react-native-wireguard-vpn';
import * as OpenVPN from '@aliakhgar1/react-native-openvpn';
import { Platform, Alert, NativeEventEmitter, NativeModules } from 'react-native';

class VpnTunnelService {
  /**
   * WireGuard config metnini kütüphanenin beklediği flat objeye çevirir
   */
  parseWireGuardConfig(configText) {
    if (!configText) return null;
    const lines = configText.split('\n');
    const config = {
      address: [],
      dns: [],
      allowedIPs: []
    };

    let section = '';
    lines.forEach(line => {
      line = line.trim();
      if (!line || line.startsWith('#')) return;

      if (line.startsWith('[Interface]')) section = 'interface';
      else if (line.startsWith('[Peer]')) section = 'peer';
      else if (line.includes('=')) {
        const parts = line.split('=');
        const key = parts[0].trim();
        const value = parts.slice(1).join('=').trim();

        if (section === 'interface') {
          if (key === 'PrivateKey') config.privateKey = value.trim();
          if (key === 'Address') {
            const addresses = value.split(',').map(s => s.trim()).filter(s => s.length > 0);
            config.address = addresses;
          }
          if (key === 'DNS') {
            config.dns = value.split(',').map(s => s.trim()).filter(s => s.length > 0);
          }
          if (key === 'MTU') {
            const parsedMtu = parseInt(value);
            config.mtu = Math.max(1280, parsedMtu);
          }
        } else if (section === 'peer') {
          if (key === 'PublicKey') config.publicKey = value.trim();
          if (key === 'AllowedIPs') {
            config.allowedIPs = value.split(',').map(s => s.trim()).filter(s => s.length > 0);
          }
          if (key === 'Endpoint') {
            const lastColonIndex = value.lastIndexOf(':');
            if (lastColonIndex !== -1) {
              config.serverAddress = value.substring(0, lastColonIndex).trim();
              config.serverPort = parseInt(value.substring(lastColonIndex + 1));
            }
          }
          if (key === 'PresharedKey') {
            config.presharedKey = value.trim();
          }
        }
      }
    });

    return config;
  }

  constructor() {
    this.isConnecting = false;
    this.setupListeners();
  }

  setupListeners() {
    if (Platform.OS === 'android' && NativeModules.Openvpn) {
      const eventEmitter = new NativeEventEmitter(NativeModules.Openvpn);
      
      // State değişikliklerini dinle
      eventEmitter.addListener('VPNStateOV', (event) => {
        console.log("LOG [OpenVPN State]:", event.state, event.message || "");
      });

      // Detaylı logları dinle
      eventEmitter.addListener('VPNLogOV', (event) => {
        console.log("LOG [OpenVPN Core]:", event.log);
      });
    }
  }

  async isWireGuardAvailable() {
    return WireGuardVPN && typeof WireGuardVPN.initialize === 'function';
  }

  async start(protocol, configText, serverIp, serverName = "GlobalShield VPN") {
    if (Platform.OS === 'web') return;
    if (this.isConnecting) {
      console.log("Already connecting, ignoring request.");
      return false;
    }

    this.isConnecting = true;
    try {
      if (protocol === 1) { // WireGuard
        return await this.startWireGuard(configText, serverName);
      } else if (protocol === 2) { // OpenVPN
        return await this.startOpenVPN(configText, serverIp, serverName);
      }
      return false;
    } finally {
      this.isConnecting = false;
    }
  }

  async startWireGuard(configText, serverName) {
    const available = await this.isWireGuardAvailable();
    if (!available) return false;

    try {
      console.log("Configuring WireGuard...");
      const config = this.parseWireGuardConfig(configText);
      
      console.log("Parsed Config Details:", {
        server: config.serverAddress,
        port: config.serverPort,
        address: config.address,
        allowedIPs: config.allowedIPs
      });

      if (!config.privateKey || !config.serverAddress) {
        throw new Error("Geçersiz VPN yapılandırması: PrivateKey veya Endpoint eksik.");
      }

      if (!config.allowedIPs || config.allowedIPs.length === 0) {
        config.allowedIPs = ["0.0.0.0/0"];
      }

      console.log("Initializing Native Backend...");
      await WireGuardVPN.initialize();

      console.log("Checking VPN Permissions...");
      const hasPermission = await WireGuardVPN.requestPermission();
      if (!hasPermission) {
        throw new Error("Lütfen VPN bağlantı isteğini onaylayın ve tekrar deneyin.");
      }
      
      console.log("Connecting to tunnel:", config.serverAddress);
      await WireGuardVPN.connect(config);
      return true;
    } catch (error) {
      console.error("WireGuard Start Error:", error);
      throw error;
    }
  }

  async startOpenVPN(configText, serverIp, serverName) {
    if (Platform.OS !== 'android') {
       console.warn("OpenVPN only implemented for Android in this version.");
       return false;
    }

    try {
      // AKILLI CONFIG TEMİZLİĞİ VE OPTİMİZASYON
      let cleanedConfig = configText
        .replace(/ignore-unknown-option block-outside-dns/g, '')
        .replace(/block-outside-dns/g, '')
        .replace(/persist-tun/g, '') // Mobilde bazen sorun çıkarabiliyor
        .replace(/verb \d+/g, 'verb 4'); // Log seviyesini artır
      
      // TCP Optimizasyonu enjekte et
      if (!cleanedConfig.includes('mssfix')) {
        cleanedConfig += '\nmssfix 1200\n';
      }

      console.log("Connecting to OpenVPN with optimized config...");
      
      const connectOptions = {
        address: serverIp || "GlobalShield",
        username: 'vpn', 
        password: 'vpn',
        openVPNConfig: cleanedConfig,
        androidOptions: {
          Notification: {
            openActivityPackageName: "com.sametarss.vpnweb.MainActivity",
            titleNotification: "GlobalShield VPN",
            titleConnected: "Connected to " + serverName
          },
          useDefaultRoute: true,
          useDefaultRouteV6: true,
          overrideDNS: true,
          DNS1: "8.8.8.8",
          DNS2: "1.1.1.1",
          compatibilityMode: 0,
          useOpenVPN3: true
        }
      };

      // Native tarafın hazır olması için çok kısa bir bekleme
      await new Promise(resolve => setTimeout(resolve, 500));

      const result = await OpenVPN.connect(connectOptions);
      console.log("OpenVPN.connect call result:", result);
      
      return true;
    } catch (error) {
      console.error("OpenVPN Start Error:", error);
      throw error;
    }
  }

  async stop() {
    try {
      // WireGuard durdur
      const wgAvailable = await this.isWireGuardAvailable();
      if (wgAvailable) {
        await WireGuardVPN.disconnect();
      }
      
      // OpenVPN durdur
      if (Platform.OS === 'android') {
        await OpenVPN.disconnect();
      }
      return true;
    } catch (error) {
      console.error("VPN Stop Error:", error);
      return true;
    }
  }

  async isWireGuardActive() {
    try {
      const state = await WireGuardVPN.getStatus();
      return state && state.isConnected === true;
    } catch (error) {
      return false;
    }
  }

  async isActive() {
    try {
      // WireGuard kontrol
      const wgAvailable = await this.isWireGuardAvailable();
      if (wgAvailable) {
        const wgActive = await this.isWireGuardActive();
        if (wgActive) return true;
      }

      // OpenVPN kontrol
      if (Platform.OS === 'android') {
        const state = await OpenVPN.getCurrentState();
        // 3: CONNECTED, 2: CONNECTING
        if (state === '3' || state === '2') return true; 
      }

      return false;
    } catch (error) {
      return false;
    }
  }
}

export default new VpnTunnelService();

