import React, { useState, useEffect, useRef } from 'react';
import { View, Text, StyleSheet, TouchableOpacity, Alert, Animated, Easing, ScrollView, Clipboard, Platform } from 'react-native';
import { MaterialCommunityIcons } from '@expo/vector-icons';
import { SafeAreaView } from 'react-native-safe-area-context';
import api from '../api/api';
import VpnTunnelService from '../services/VpnTunnelService';

const ConnectedScreen = ({ navigation, route }) => {
  const [duration, setDuration] = useState(0);
  const [ipAddress, setIpAddress] = useState(route.params?.ipAddress || '---.---.---.---');
  const [serverName, setServerName] = useState(route.params?.serverName || 'VPN Server');
  const [protocol, setProtocol] = useState(route.params?.protocol === 1 ? 'WireGuard' : 'OpenVPN');
  const [config, setConfig] = useState(route.params?.clientConfig || '');
  const [showConfig, setShowConfig] = useState(false);
  
  const timerRef = useRef(null);
  const fadeAnim = useRef(new Animated.Value(0)).current;

  useEffect(() => {
    Animated.timing(fadeAnim, {
      toValue: 1,
      duration: 800,
      useNativeDriver: true,
    }).start();

    const startTime = new Date(route.params?.connectedAt || new Date()).getTime();
    const updateTimer = () => {
      const now = new Date().getTime();
      setDuration(Math.floor((now - startTime) / 1000));
    };

    updateTimer();
    timerRef.current = setInterval(updateTimer, 1000);

    return () => clearInterval(timerRef.current);
  }, []);

  const formatDuration = (seconds) => {
    const h = Math.floor(seconds / 3600);
    const m = Math.floor((seconds % 3600) / 60);
    const s = seconds % 60;
    return `${h > 0 ? h + ':' : ''}${m < 10 && h > 0 ? '0' : ''}${m}:${s < 10 ? '0' : ''}${s}`;
  };

  const copyToClipboard = () => {
    Clipboard.setString(config);
    Alert.alert('Success', 'Config copied to clipboard!');
  };

  const handleDisconnect = async () => {
    try {
      // 1. Native tüneli durdur
      await VpnTunnelService.stop();
      
      // 2. Backend bağlantısını kes
      await api.post('/connect/disconnect');
      navigation.replace('HomeMain');
    } catch (error) {
      Alert.alert('Hata', 'Bağlantı kesilemedi.');
    }
  };

  return (
    <SafeAreaView style={styles.container}>
      <ScrollView contentContainerStyle={styles.scrollContent}>
        <Animated.View style={[styles.content, { opacity: fadeAnim }]}>
          <View style={styles.header}>
            <MaterialCommunityIcons name="shield-check" size={80} color="#10b981" />
            <Text style={styles.title}>Connection Active</Text>
            <Text style={styles.subtitle}>Your connection is established on the server</Text>
          </View>

          <View style={styles.infoAlert}>
            <MaterialCommunityIcons name="information-outline" size={20} color="#3b82f6" />
            <Text style={styles.infoAlertText}>
              To route your device traffic, copy the config below and paste it into the {protocol} app.
            </Text>
          </View>

          <View style={styles.statsContainer}>
            <View style={styles.statBox}>
              <Text style={styles.statLabel}>PUBLIC IP</Text>
              <Text style={styles.statValue}>{ipAddress}</Text>
            </View>
            <View style={styles.divider} />
            <View style={styles.statBox}>
              <Text style={styles.statLabel}>DURATION</Text>
              <Text style={styles.statValue}>{formatDuration(duration)}</Text>
            </View>
          </View>

          <TouchableOpacity style={styles.configHeader} onPress={() => setShowConfig(!showConfig)}>
            <Text style={styles.configHeaderTitle}>Client Configuration</Text>
            <MaterialCommunityIcons name={showConfig ? "chevron-up" : "chevron-down"} size={24} color="#3b82f6" />
          </TouchableOpacity>

          {showConfig && (
            <View style={styles.configContainer}>
              <Text style={styles.configText} numberOfLines={8}>{config}</Text>
              <TouchableOpacity style={styles.copyButton} onPress={copyToClipboard}>
                <MaterialCommunityIcons name="content-copy" size={20} color="#fff" />
                <Text style={styles.copyButtonText}>Copy Config</Text>
              </TouchableOpacity>
            </View>
          )}

          <TouchableOpacity style={styles.disconnectButton} onPress={handleDisconnect}>
            <MaterialCommunityIcons name="power" size={24} color="#fff" />
            <Text style={styles.disconnectText}>Terminate Session</Text>
          </TouchableOpacity>

          <TouchableOpacity style={styles.backButton} onPress={() => navigation.navigate('HomeMain')}>
            <Text style={styles.backButtonText}>Back to Dashboard</Text>
          </TouchableOpacity>
        </Animated.View>
      </ScrollView>
    </SafeAreaView>
  );
};

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: '#0f172a' },
  scrollContent: { flexGrow: 1 },
  content: { flex: 1, padding: 25, alignItems: 'center' },
  header: { alignItems: 'center', marginBottom: 30, marginTop: 20 },
  title: { color: '#fff', fontSize: 28, fontWeight: 'bold', marginTop: 15 },
  subtitle: { color: 'rgba(255,255,255,0.5)', fontSize: 14, textAlign: 'center', marginTop: 8 },
  
  infoAlert: { 
    flexDirection: 'row', 
    backgroundColor: 'rgba(59, 130, 246, 0.1)', 
    padding: 15, 
    borderRadius: 15, 
    marginBottom: 25,
    alignItems: 'center'
  },
  infoAlertText: { color: '#3b82f6', fontSize: 12, marginLeft: 10, flex: 1, fontWeight: '500' },

  statsContainer: { 
    width: '100%', 
    backgroundColor: 'rgba(255,255,255,0.03)', 
    borderRadius: 20, 
    padding: 20,
    borderWidth: 1,
    borderColor: 'rgba(255,255,255,0.05)',
    marginBottom: 20
  },
  statBox: { paddingVertical: 5 },
  statLabel: { color: 'rgba(255,255,255,0.4)', fontSize: 10, fontWeight: 'bold', letterSpacing: 1 },
  statValue: { color: '#fff', fontSize: 18, fontWeight: '600', marginTop: 4 },
  divider: { height: 1, backgroundColor: 'rgba(255,255,255,0.05)', marginVertical: 12 },

  configHeader: { 
    flexDirection: 'row', 
    justifyContent: 'space-between', 
    alignItems: 'center', 
    width: '100%',
    padding: 15,
    backgroundColor: 'rgba(255,255,255,0.02)',
    borderRadius: 12,
    marginBottom: 15
  },
  configHeaderTitle: { color: '#fff', fontSize: 15, fontWeight: '600' },
  
  configContainer: { 
    width: '100%', 
    backgroundColor: '#000', 
    padding: 15, 
    borderRadius: 12, 
    marginBottom: 25 
  },
  configText: { color: '#4ade80', fontSize: 11, fontFamily: Platform.OS === 'ios' ? 'Courier' : 'monospace' },
  copyButton: { 
    flexDirection: 'row', 
    alignItems: 'center', 
    justifyContent: 'center', 
    backgroundColor: '#3b82f6', 
    padding: 10, 
    borderRadius: 8, 
    marginTop: 15 
  },
  copyButtonText: { color: '#fff', fontSize: 14, fontWeight: 'bold', marginLeft: 8 },

  disconnectButton: {
    backgroundColor: '#ef4444',
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    paddingVertical: 16,
    borderRadius: 15,
    width: '100%',
    marginTop: 10
  },
  disconnectText: { color: '#fff', fontSize: 16, fontWeight: 'bold', marginLeft: 10 },
  
  backButton: { marginVertical: 20 },
  backButtonText: { color: 'rgba(255,255,255,0.4)', fontSize: 14 }
});

export default ConnectedScreen;

