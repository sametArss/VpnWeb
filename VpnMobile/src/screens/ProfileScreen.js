import React, { useEffect, useState } from 'react';
import { View, Text, StyleSheet, TouchableOpacity, ScrollView, ActivityIndicator } from 'react-native';
import { MaterialCommunityIcons } from '@expo/vector-icons';
import AsyncStorage from '@react-native-async-storage/async-storage';
import api from '../api/api';

const ProfileScreen = ({ onLogout }) => {
  const [user, setUser] = useState(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    fetchProfile();
  }, []);

  const fetchProfile = async () => {
    try {
      const response = await api.get('/users/profile');
      setUser(response.data);
    } catch (error) {
      console.error(error);
    } finally {
      setLoading(false);
    }
  };

  const handleLogout = async () => {
    await AsyncStorage.removeItem('token');
    onLogout();
  };

  const MenuItem = ({ icon, title, subtitle, color = "#fff" }) => (
    <TouchableOpacity style={styles.menuItem}>
      <View style={[styles.iconBg, { backgroundColor: 'rgba(255,255,255,0.05)' }]}>
        <MaterialCommunityIcons name={icon} size={24} color="#3b82f6" />
      </View>
      <View style={styles.menuContent}>
        <Text style={[styles.menuTitle, { color }]}>{title}</Text>
        {subtitle && <Text style={styles.menuSubtitle}>{subtitle}</Text>}
      </View>
      <MaterialCommunityIcons name="chevron-right" size={20} color="rgba(255,255,255,0.2)" />
    </TouchableOpacity>
  );

  if (loading) return <ActivityIndicator style={{flex:1, backgroundColor: '#0f172a'}} color="#3b82f6" />;

  return (
    <ScrollView style={styles.container}>
      <View style={styles.header}>
        <View style={styles.avatarContainer}>
          <Text style={styles.avatarText}>{user?.fullName?.charAt(0) || 'U'}</Text>
        </View>
        <Text style={styles.userName}>{user?.fullName || 'User Name'}</Text>
        <Text style={styles.userEmail}>{user?.email}</Text>
      </View>

      <View style={styles.section}>
        <Text style={styles.sectionTitle}>Account Settings</Text>
        <MenuItem icon="account-outline" title="Personal Info" subtitle="Update your name and email" />
        <MenuItem icon="shield-lock-outline" title="Security" subtitle="Change password, 2FA" />
      </View>

      <View style={styles.section}>
        <Text style={styles.sectionTitle}>Information</Text>
        <MenuItem icon="information-outline" title="About Us" />
        <MenuItem icon="file-document-outline" title="Privacy Policy" />
        <MenuItem icon="help-circle-outline" title="Support" />
      </View>

      <TouchableOpacity style={styles.logoutButton} onPress={handleLogout}>
        <MaterialCommunityIcons name="logout" size={20} color="#f87171" style={{marginRight: 10}} />
        <Text style={styles.logoutButtonText}>Log Out</Text>
      </TouchableOpacity>
      
      <Text style={styles.version}>GlobalShield v1.0.0</Text>
    </ScrollView>
  );
};

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: '#0f172a' },
  header: { alignItems: 'center', marginTop: 60, marginBottom: 30 },
  avatarContainer: { width: 80, height: 80, borderRadius: 40, backgroundColor: '#3b82f6', justifyContent: 'center', alignItems: 'center', marginBottom: 15 },
  avatarText: { fontSize: 32, fontWeight: 'bold', color: '#fff' },
  userName: { fontSize: 22, fontWeight: 'bold', color: '#fff' },
  userEmail: { color: 'rgba(255,255,255,0.5)', marginTop: 5 },
  section: { paddingHorizontal: 20, marginBottom: 30 },
  sectionTitle: { color: 'rgba(255,255,255,0.3)', fontSize: 12, fontWeight: 'bold', letterSpacing: 1, marginBottom: 15, textTransform: 'uppercase' },
  menuItem: { flexDirection: 'row', alignItems: 'center', marginBottom: 15, backgroundColor: 'rgba(255,255,255,0.02)', padding: 15, borderRadius: 15 },
  iconBg: { width: 45, height: 45, borderRadius: 12, justifyContent: 'center', alignItems: 'center', marginRight: 15 },
  menuContent: { flex: 1 },
  menuTitle: { fontSize: 16, fontWeight: '500' },
  menuSubtitle: { fontSize: 12, color: 'rgba(255,255,255,0.4)', marginTop: 2 },
  logoutButton: { flexDirection: 'row', justifyContent: 'center', alignItems: 'center', marginHorizontal: 20, padding: 18, borderRadius: 15, backgroundColor: 'rgba(248,113,113,0.1)', marginBottom: 20 },
  logoutButtonText: { color: '#f87171', fontSize: 16, fontWeight: 'bold' },
  version: { textAlign: 'center', color: 'rgba(255,255,255,0.2)', fontSize: 12, marginBottom: 40 }
});

export default ProfileScreen;
