import React, { useEffect, useState } from 'react';
import { View, Text, FlatList, StyleSheet, ActivityIndicator, TouchableOpacity, Alert } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { MaterialCommunityIcons } from '@expo/vector-icons';
import AsyncStorage from '@react-native-async-storage/async-storage';
import api from '../api/api';
import VpnTunnelService from '../services/VpnTunnelService';

const ServerListScreen = ({ navigation }) => {
  const [servers, setServers] = useState([]);
  const [loading, setLoading] = useState(true);
  const [activeConnection, setActiveConnection] = useState(null);

  const fetchStatusAndServers = async (showLoader = false) => {
    if (showLoader) setLoading(true);
    
    // 1. Sunucuları getir
    try {
      const serversResponse = await api.get('/servers');
      setServers(serversResponse.data);
    } catch (serversError) {
      console.warn("Sunucular listesi alınamadı (VPN aktif olduğundan yerel ağa erişilemiyor olabilir):", serversError.message);
    }

    // 2. Bağlantı durumunu getir
    try {
      const statusResponse = await api.get('/connect/status');
      if (statusResponse.data && statusResponse.data.isConnected) {
        setActiveConnection(statusResponse.data);
        await AsyncStorage.setItem('active_vpn_connection', JSON.stringify(statusResponse.data));
      } else {
        setActiveConnection(null);
        await AsyncStorage.removeItem('active_vpn_connection');
      }
    } catch (statusError) {
      console.warn("Bağlantı durumu API'den alınamadı, yerel kontrole geçiliyor:", statusError.message);
      // Fallback: Yerel cihazdaki tünel durumunu kontrol et
      try {
        const isNativeActive = await VpnTunnelService.isActive();
        if (isNativeActive) {
          const savedRaw = await AsyncStorage.getItem('active_vpn_connection');
          if (savedRaw) {
            setActiveConnection(JSON.parse(savedRaw));
          } else {
            setActiveConnection(null);
          }
        } else {
          setActiveConnection(null);
          await AsyncStorage.removeItem('active_vpn_connection');
        }
      } catch (nativeErr) {
        console.error("Yerel tünel kontrolü başarısız:", nativeErr);
        setActiveConnection(null);
      }
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    const unsubscribe = navigation.addListener('focus', () => {
      fetchStatusAndServers(servers.length === 0);
    });
    return unsubscribe;
  }, [navigation, servers.length]);

  const handleDisconnect = async () => {
    try {
      setLoading(true);
      await VpnTunnelService.stop();
      try {
        await api.post('/connect/disconnect');
      } catch (e) {}
      await AsyncStorage.removeItem('active_vpn_connection');
      await fetchStatusAndServers(true);
      Alert.alert('Bağlantı Kesildi', 'VPN bağlantısı başarıyla sonlandırıldı.');
    } catch (error) {
      Alert.alert('Hata', 'Bağlantı kesilirken bir hata oluştu.');
      setLoading(false);
    }
  };

  const handleServerSelect = (item) => {
    if (!item.isActive) {
      Alert.alert('Sunucu Çevrimdışı', 'Bu sunucu şu anda aktif değil. Lütfen başka bir sunucu seçin.');
      return;
    }

    const isCurrentActive = activeConnection && activeConnection.isConnected && activeConnection.ipAddress === item.ipAddress;
    if (isCurrentActive) {
      Alert.alert(
        'Bağlantıyı Kes',
        `${item.name} sunucusuyla olan bağlantıyı kesmek istiyor musunuz?`,
        [
          { text: 'İptal', style: 'cancel' },
          { text: 'Bağlantıyı Kes', onPress: handleDisconnect, style: 'destructive' }
        ]
      );
      return;
    }

    // Home tab'ine yönlendir ve seçilen sunucu bilgilerini parametre olarak ilet
    navigation.navigate('Home', {
      screen: 'HomeMain',
      params: {
        autoConnectServer: {
          id: item.id,
          name: item.name
        }
      }
    });
  };

  const renderItem = ({ item }) => {
    const isCurrentActive = activeConnection && activeConnection.isConnected && activeConnection.ipAddress === item.ipAddress;

    return (
      <TouchableOpacity 
        style={[
          styles.serverCard,
          isCurrentActive && styles.activeServerCard
        ]}
        onPress={() => handleServerSelect(item)}
      >
        <View style={styles.serverInfo}>
          <View style={[
            styles.flagContainer,
            isCurrentActive && { backgroundColor: 'rgba(239, 68, 68, 0.1)' }
          ]}>
            <MaterialCommunityIcons 
              name="earth" 
              size={24} 
              color={isCurrentActive ? '#ef4444' : '#3b82f6'} 
            />
          </View>
          <View>
            <Text style={styles.serverName}>{item.name}</Text>
            <Text style={styles.serverIp}>{item.ipAddress}</Text>
            {item.isActive && (
              <Text style={[
                styles.connectHint,
                isCurrentActive ? styles.activeConnectHint : null
              ]}>
                {isCurrentActive ? 'Bağlantıyı kesmek için tıklayın' : 'Bağlanmak için tıklayın'}
              </Text>
            )}
          </View>
        </View>
        <View style={[
          styles.statusBadge, 
          { 
            backgroundColor: isCurrentActive 
              ? 'rgba(239, 68, 68, 0.15)' 
              : item.isActive 
                ? 'rgba(74, 222, 128, 0.15)' 
                : 'rgba(248, 113, 113, 0.1)', 
            flexDirection: 'row', 
            alignItems: 'center' 
          }
        ]}>
          <MaterialCommunityIcons 
            name="power" 
            size={14} 
            color={
              isCurrentActive 
                ? '#ef4444' 
                : item.isActive 
                  ? '#4ade80' 
                  : '#f87171'
            } 
            style={{ marginRight: 4 }} 
          />
          <Text style={[
            styles.statusText, 
            { 
              color: isCurrentActive 
                ? '#ef4444' 
                : item.isActive 
                  ? '#4ade80' 
                  : '#f87171' 
            }
          ]}>
            {isCurrentActive ? 'Bağlantıyı Kes' : item.isActive ? 'Bağlan' : 'Çevrimdışı'}
          </Text>
        </View>
      </TouchableOpacity>
    );
  };

  return (
    <SafeAreaView style={styles.container}>
      <View style={styles.header}>
        <Text style={styles.title}>VPN Locations</Text>
        <Text style={styles.subtitle}>Select the fastest server for you</Text>
      </View>

      {loading ? (
        <ActivityIndicator size="large" color="#3b82f6" style={{marginTop: 50}} />
      ) : (
        <FlatList
          data={servers}
          keyExtractor={(item) => item.id.toString()}
          renderItem={renderItem}
          contentContainerStyle={styles.list}
          showsVerticalScrollIndicator={false}
        />
      )}
    </SafeAreaView>
  );
};

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: '#0f172a' },
  header: { paddingHorizontal: 25, marginTop: 40, marginBottom: 20 },
  title: { fontSize: 28, fontWeight: 'bold', color: '#fff' },
  subtitle: { color: 'rgba(255,255,255,0.4)', fontSize: 14, marginTop: 5 },
  list: { paddingHorizontal: 25, paddingBottom: 100 },
  serverCard: { 
    backgroundColor: 'rgba(255,255,255,0.03)', 
    padding: 18, 
    borderRadius: 18, 
    marginBottom: 15, 
    flexDirection: 'row', 
    justifyContent: 'space-between', 
    alignItems: 'center',
    borderWidth: 1,
    borderColor: 'rgba(255,255,255,0.05)'
  },
  activeServerCard: {
    borderColor: 'rgba(239, 68, 68, 0.3)',
    backgroundColor: 'rgba(239, 68, 68, 0.02)'
  },
  serverInfo: { flexDirection: 'row', alignItems: 'center' },
  flagContainer: { width: 45, height: 45, borderRadius: 12, backgroundColor: 'rgba(59, 130, 246, 0.1)', justifyContent: 'center', alignItems: 'center', marginRight: 15 },
  serverName: { fontSize: 16, fontWeight: 'bold', color: '#fff' },
  serverIp: { color: 'rgba(255,255,255,0.4)', marginTop: 2, fontSize: 13 },
  connectHint: { color: 'rgba(74, 222, 128, 0.7)', fontSize: 11, marginTop: 4 },
  activeConnectHint: { color: 'rgba(239, 68, 68, 0.7)' },
  statusBadge: { paddingVertical: 6, paddingHorizontal: 12, borderRadius: 10 },
  statusText: { fontWeight: 'bold', fontSize: 12 }
});

export default ServerListScreen;
