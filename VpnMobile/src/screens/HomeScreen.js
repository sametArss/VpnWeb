import React, { useState, useEffect, useRef } from 'react';
import { View, Text, StyleSheet, TouchableOpacity, Animated, Easing, Alert, ScrollView, Platform } from 'react-native';
import { MaterialCommunityIcons } from '@expo/vector-icons';
import { SafeAreaView } from 'react-native-safe-area-context';
import AsyncStorage from '@react-native-async-storage/async-storage';
import api from '../api/api';
import VpnTunnelService from '../services/VpnTunnelService';

const HomeScreen = ({ navigation, route }) => {
  const [isConnected, setIsConnected] = useState(false);
  const [connecting, setConnecting] = useState(false);
  const [protocol, setProtocol] = useState(1); // 1: WireGuard (default), 2: OpenVPN
  const [serverName, setServerName] = useState('Auto Select');
  const [ipAddress, setIpAddress] = useState('');
  const [connectedAt, setConnectedAt] = useState('');
  const [clientConfig, setClientConfig] = useState('');
  
  const pulseAnim = useRef(new Animated.Value(1)).current;

  useEffect(() => {
    if (Platform.OS === 'android' && Platform.Version >= 33) {
      const requestNotificationPermission = async () => {
        try {
          const { PermissionsAndroid } = require('react-native');
          await PermissionsAndroid.request(
            PermissionsAndroid.PERMISSIONS.POST_NOTIFICATIONS
          );
        } catch (err) {
          console.warn(err);
        }
      };
      requestNotificationPermission();
    }
    
    const unsubscribe = navigation.addListener('focus', () => {
      checkStatus();
    });
    return unsubscribe;
  }, [navigation]);

  useEffect(() => {
    const handleAutoConnect = async () => {
      if (route.params?.autoConnectServer) {
        const { id, name } = route.params.autoConnectServer;
        
        // Parametreyi hemen temizle ki tekrar focus olunca yeniden bağlanmaya çalışmasın
        navigation.setParams({ autoConnectServer: undefined });

        if (isConnected) {
          // Eğer zaten bağlıysa ve seçilen sunucu farklıysa kullanıcıya sor
          if (serverName !== name) {
            Alert.alert(
              'Sunucu Değiştir',
              `Mevcut sunucu bağlantısını kesip ${name} sunucusuna bağlanmak istiyor musunuz?`,
              [
                { text: 'İptal', style: 'cancel' },
                { 
                  text: 'Bağlan', 
                  onPress: async () => {
                    setConnecting(true);
                    try {
                      await VpnTunnelService.stop();
                      try {
                        await api.post('/connect/disconnect');
                      } catch (e) {}
                      setIsConnected(false);
                      await AsyncStorage.removeItem('active_vpn_connection');
                      await initiateConnection(id, name);
                    } catch (err) {
                      Alert.alert('Hata', 'Mevcut bağlantı kesilemedi.');
                      setConnecting(false);
                    }
                  } 
                }
              ]
            );
          }
        } else {
          await initiateConnection(id, name);
        }
      }
    };

    handleAutoConnect();
  }, [route.params?.autoConnectServer, isConnected, serverName]);

  useEffect(() => {
    if (connecting) {
      startPulse();
    } else {
      pulseAnim.setValue(1);
    }
  }, [connecting]);

  const startPulse = () => {
    Animated.loop(
      Animated.sequence([
        Animated.timing(pulseAnim, {
          toValue: 1.2,
          duration: 1000,
          easing: Easing.inOut(Easing.ease),
          useNativeDriver: true,
        }),
        Animated.timing(pulseAnim, {
          toValue: 1,
          duration: 1000,
          easing: Easing.inOut(Easing.ease),
          useNativeDriver: true,
        }),
      ])
    ).start();
  };

  const checkStatus = async () => {
    try {
      const response = await api.get('/connect/status');
      if (response.data.isConnected) {
        setIsConnected(true);
        setServerName(response.data.name);
        setProtocol(response.data.protocol);
        setIpAddress(response.data.ipAddress);
        setConnectedAt(response.data.connectedAt);
        setClientConfig(response.data.clientConfig);

        await AsyncStorage.setItem('active_vpn_connection', JSON.stringify({
          isConnected: true,
          name: response.data.name,
          ipAddress: response.data.ipAddress,
          protocol: response.data.protocol,
          connectedAt: response.data.connectedAt,
          clientConfig: response.data.clientConfig
        }));

        // Zaten bağlıysa direkt detay ekranına atabiliriz
        navigation.navigate('Connected', {
          ipAddress: response.data.ipAddress,
          serverName: response.data.name,
          protocol: response.data.protocol,
          connectedAt: response.data.connectedAt,
          clientConfig: response.data.clientConfig
        });
      } else {
        setIsConnected(false);
        setServerName('Auto Select');
        setIpAddress('');
        setConnectedAt('');
        setClientConfig('');
        await AsyncStorage.removeItem('active_vpn_connection');
      }
    } catch (error) {
      // API isteği başarısız olursa (örn: VPN yerel bilgisayardaki API'yi engellerse) cihazın kendi VPN durumunu kontrol et
      try {
        const isNativeActive = await VpnTunnelService.isActive();
        if (isNativeActive) {
          const savedRaw = await AsyncStorage.getItem('active_vpn_connection');
          if (savedRaw) {
            const saved = JSON.parse(savedRaw);
            setIsConnected(true);
            setServerName(saved.name);
            setProtocol(saved.protocol);
            setIpAddress(saved.ipAddress);
            setConnectedAt(saved.connectedAt);
            setClientConfig(saved.clientConfig);
            return;
          }
        }
      } catch (nativeErr) {
        console.error('Native status check error:', nativeErr);
      }

      if (error.response?.status !== 401) {
        console.error('Status check error:', error);
      }
    }
  };

  const initiateConnection = async (targetServerId = 0, targetServerName = null) => {
    setConnecting(true);
    const finalServerName = targetServerName || serverName;
    try {
      // 1. Backend'den config al
      const response = await api.post('/connect/connect', {
        protocol: protocol,
        serverId: targetServerId
      });

      // 2. Telefonda gerçek VPN tünelini başlat
      try {
        console.log(`${protocol === 1 ? 'WireGuard' : 'OpenVPN'} tunnel starting with IP: ${response.data.ipAddress}`);
        const success = await VpnTunnelService.start(protocol, response.data.clientConfig, response.data.ipAddress, finalServerName);
        
        if (!success) {
           // success false dönerse (örneğin izin diyaloğu açıldıysa) akışı durduruyoruz
           setConnecting(false);
           return;
        }
      } catch (vpnError) {
        console.error("VPN Start Error:", vpnError);
        Alert.alert('VPN Hatası', 'VPN bağlantısı kurulamadı: ' + vpnError.message);
        setConnecting(false);
        return;
      }

      setIsConnected(true);
      setServerName(finalServerName);
      setIpAddress(response.data.ipAddress);
      setConnectedAt(response.data.connectedAt);
      setClientConfig(response.data.clientConfig);

      await AsyncStorage.setItem('active_vpn_connection', JSON.stringify({
        isConnected: true,
        name: finalServerName,
        ipAddress: response.data.ipAddress,
        protocol: response.data.protocol,
        connectedAt: response.data.connectedAt,
        clientConfig: response.data.clientConfig
      }));

      navigation.navigate('Connected', {
        ipAddress: response.data.ipAddress,
        serverName: finalServerName,
        protocol: response.data.protocol,
        connectedAt: response.data.connectedAt,
        clientConfig: response.data.clientConfig
      });
    } catch (error) {
      const msg = error.response?.data?.message || 'Bağlantı hatası oluştu.';
      Alert.alert('Hata', msg);
    } finally {
      setConnecting(false);
    }
  };

  const handleConnect = async () => {
    if (isConnected) {
      handleDisconnect();
      return;
    }
    await initiateConnection(0);
  };

  const handleDisconnect = async () => {
    setConnecting(true);
    try {
      // 1. Native tüneli durdur (eğer aktifse)
      await VpnTunnelService.stop();

      // 2. Backend bağlantısını kes
      try {
        await api.post('/connect/disconnect');
      } catch (apiError) {
        // API hatası olsa bile yerel durumu sıfırlıyoruz
        console.warn("API disconnect failed, but local state reset.");
      }

      setIsConnected(false);
      setServerName('Auto Select');
      setIpAddress('');
      setConnectedAt('');
      setClientConfig('');
      await AsyncStorage.removeItem('active_vpn_connection');
    } catch (error) {
      Alert.alert('Hata', 'Bağlantı kesilirken bir hata oluştu.');
    } finally {
      setConnecting(false);
    }
  };

  return (
    <SafeAreaView style={styles.container}>
      <ScrollView 
        contentContainerStyle={styles.scrollContent}
        showsVerticalScrollIndicator={false}
      >
        <View style={styles.header}>
          <Text style={styles.title}>GlobalShield</Text>
          <View style={styles.statusBadge}>
            <View style={[styles.dot, { backgroundColor: isConnected ? '#4ade80' : '#f87171' }]} />
            <Text style={styles.statusText}>{isConnected ? 'SECURED' : 'UNPROTECTED'}</Text>
          </View>
        </View>

        <View style={styles.centerContent}>
          <Animated.View style={[styles.pulseContainer, { transform: [{ scale: pulseAnim }] }]}>
            <TouchableOpacity 
              style={[styles.connectButton, isConnected ? styles.connectedButton : null]} 
              onPress={handleConnect}
              disabled={connecting}
            >
              <MaterialCommunityIcons 
                name={isConnected ? "shield-check" : "power"} 
                size={80} 
                color="#fff" 
              />
            </TouchableOpacity>
          </Animated.View>
          
          <View style={styles.statusLabelContainer}>
            <Text style={styles.connectText}>
              {connecting ? 'Processing...' : isConnected ? 'Disconnect VPN' : 'Tap to Connect'}
            </Text>
            <Text style={styles.serverText}>{serverName}</Text>
          </View>

          {isConnected && (
            <TouchableOpacity 
              style={styles.detailsButton} 
              onPress={() => navigation.navigate('Connected', { 
                ipAddress, 
                serverName, 
                protocol, 
                connectedAt, 
                clientConfig 
              })}
            >
              <Text style={styles.detailsButtonText}>View Connection Details</Text>
              <MaterialCommunityIcons name="chevron-right" size={20} color="#3b82f6" />
            </TouchableOpacity>
          )}
        </View>

        {!isConnected && (
          <View style={styles.protocolContainer}>
            <Text style={styles.protocolLabel}>Connection Protocol</Text>
            <View style={styles.protocolButtons}>
              {[
                { id: 2, name: 'OpenVPN' },
                { id: 1, name: 'WireGuard' }
              ].map((p) => (
                <TouchableOpacity 
                  key={p.id} 
                  style={[styles.protocolButton, protocol === p.id ? styles.activeProtocol : null]}
                  onPress={() => setProtocol(p.id)}
                >
                  <Text style={[styles.protocolText, protocol === p.id ? styles.activeProtocolText : null]}>{p.name}</Text>
                </TouchableOpacity>
              ))}
            </View>
          </View>
        )}
      </ScrollView>
    </SafeAreaView>
  );
};

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: '#0f172a' },
  scrollContent: { padding: 20, flexGrow: 1, justifyContent: 'center' },
  header: { position: 'absolute', top: 20, left: 20, right: 20, alignItems: 'center' },
  title: { fontSize: 28, fontWeight: 'bold', color: '#fff' },
  statusBadge: { flexDirection: 'row', alignItems: 'center', marginTop: 10, backgroundColor: 'rgba(255,255,255,0.05)', paddingHorizontal: 15, paddingVertical: 5, borderRadius: 20 },
  dot: { width: 8, height: 8, borderRadius: 4, marginRight: 8 },
  statusText: { color: 'rgba(255,255,255,0.6)', fontSize: 12, fontWeight: 'bold' },
  
  centerContent: { alignItems: 'center', marginVertical: 60 },
  pulseContainer: { padding: 10 },
  connectButton: { 
    width: 200, 
    height: 200, 
    borderRadius: 100, 
    backgroundColor: '#3b82f6', 
    justifyContent: 'center', 
    alignItems: 'center',
    ...Platform.select({
      ios: {
        shadowColor: '#3b82f6',
        shadowOffset: { width: 0, height: 0 },
        shadowOpacity: 0.5,
        shadowRadius: 30,
      },
      android: {
        elevation: 20,
      }
    })
  },
  connectedButton: { 
    backgroundColor: '#ef4444',
    shadowColor: '#ef4444',
  },
  statusLabelContainer: { alignItems: 'center', marginTop: 30 },
  connectText: { color: '#fff', fontSize: 22, fontWeight: '600' },
  serverText: { color: 'rgba(255,255,255,0.5)', fontSize: 16, marginTop: 8 },

  detailsButton: { 
    flexDirection: 'row', 
    alignItems: 'center', 
    marginTop: 20, 
    backgroundColor: 'rgba(59, 130, 246, 0.1)', 
    paddingVertical: 10, 
    paddingHorizontal: 20, 
    borderRadius: 20 
  },
  detailsButtonText: { color: '#3b82f6', fontWeight: 'bold', marginRight: 5 },

  protocolContainer: { marginTop: 40 },
  protocolLabel: { color: 'rgba(255,255,255,0.4)', textAlign: 'center', marginBottom: 20, fontSize: 12, fontWeight: 'bold', textTransform: 'uppercase', letterSpacing: 1 },
  protocolButtons: { flexDirection: 'row', justifyContent: 'center' },
  protocolButton: { paddingVertical: 12, paddingHorizontal: 30, borderRadius: 16, borderWidth: 1, borderColor: 'rgba(255,255,255,0.1)', marginHorizontal: 10 },
  activeProtocol: { backgroundColor: '#3b82f6', borderColor: '#3b82f6' },
  protocolText: { color: 'rgba(255,255,255,0.5)', fontWeight: 'bold' },
  activeProtocolText: { color: '#fff' }
});

export default HomeScreen;



