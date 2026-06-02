import React, { useEffect, useState } from 'react';
import { View, Text, FlatList, StyleSheet, ActivityIndicator, TouchableOpacity } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { MaterialCommunityIcons } from '@expo/vector-icons';
import api from '../api/api';

const ServerListScreen = () => {
  const [servers, setServers] = useState([]);
  const [loading, setLoading] = useState(true);

  const fetchServers = async () => {
    try {
      const response = await api.get('/servers');
      setServers(response.data);
    } catch (error) {
      console.error(error);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchServers();
  }, []);

  const renderItem = ({ item }) => (
    <TouchableOpacity style={styles.serverCard}>
      <View style={styles.serverInfo}>
        <View style={styles.flagContainer}>
          <MaterialCommunityIcons name="earth" size={24} color="#3b82f6" />
        </View>
        <View>
          <Text style={styles.serverName}>{item.name}</Text>
          <Text style={styles.serverIp}>{item.ipAddress}</Text>
        </View>
      </View>
      <View style={[styles.statusBadge, { backgroundColor: item.isActive ? 'rgba(74, 222, 128, 0.1)' : 'rgba(248, 113, 113, 0.1)' }]}>
        <Text style={[styles.statusText, { color: item.isActive ? '#4ade80' : '#f87171' }]}>
          {item.isActive ? 'Online' : 'Offline'}
        </Text>
      </View>
    </TouchableOpacity>
  );

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
  serverInfo: { flexDirection: 'row', alignItems: 'center' },
  flagContainer: { width: 45, height: 45, borderRadius: 12, backgroundColor: 'rgba(59, 130, 246, 0.1)', justifyContent: 'center', alignItems: 'center', marginRight: 15 },
  serverName: { fontSize: 16, fontWeight: 'bold', color: '#fff' },
  serverIp: { color: 'rgba(255,255,255,0.4)', marginTop: 2, fontSize: 13 },
  statusBadge: { paddingVertical: 6, paddingHorizontal: 12, borderRadius: 10 },
  statusText: { fontWeight: 'bold', fontSize: 12 }
});

export default ServerListScreen;
