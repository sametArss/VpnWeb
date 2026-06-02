import React, { useState, useEffect } from 'react';
import { NavigationContainer } from '@react-navigation/native';
import { createBottomTabNavigator } from '@react-navigation/bottom-tabs';
import { createNativeStackNavigator } from '@react-navigation/native-stack';
import { MaterialCommunityIcons } from '@expo/vector-icons';
import AsyncStorage from '@react-native-async-storage/async-storage';
import { SafeAreaProvider } from 'react-native-safe-area-context';

import LoginScreen from './src/screens/LoginScreen';
import HomeScreen from './src/screens/HomeScreen';
import ServerListScreen from './src/screens/ServerListScreen';
import ProfileScreen from './src/screens/ProfileScreen';
import ConnectedScreen from './src/screens/ConnectedScreen';

const Tab = createBottomTabNavigator();
const Stack = createNativeStackNavigator();

function HomeStack() {
  return (
    <Stack.Navigator screenOptions={{ headerShown: false }}>
      <Stack.Screen name="HomeMain" component={HomeScreen} />
      <Stack.Screen name="Connected" component={ConnectedScreen} />
    </Stack.Navigator>
  );
}

export default function App() {
  const [isLoggedIn, setIsLoggedIn] = useState(false);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    checkLoginStatus();
  }, []);

  const checkLoginStatus = async () => {
    const token = await AsyncStorage.getItem('token');
    if (token) {
      setIsLoggedIn(true);
    }
    setLoading(false);
  };

  if (loading) return null;

  if (!isLoggedIn) {
    return <LoginScreen onLoginSuccess={() => setIsLoggedIn(true)} />;
  }

  return (
    <SafeAreaProvider>
      <NavigationContainer>
        <Tab.Navigator
          screenOptions={({ route }) => ({
            tabBarIcon: ({ color, size }) => {
              let iconName;
              if (route.name === 'Home') iconName = 'shield-home';
              else if (route.name === 'Servers') iconName = 'server-network';
              else if (route.name === 'Profile') iconName = 'account-circle';
              return <MaterialCommunityIcons name={iconName} size={size} color={color} />;
            },
            tabBarStyle: { 
              backgroundColor: '#0f172a', 
              borderTopWidth: 0,
              height: 70,
              paddingBottom: 12
            },
            tabBarActiveTintColor: '#3b82f6',
            tabBarInactiveTintColor: 'rgba(255,255,255,0.3)',
            headerShown: false,
          })}
        >
          <Tab.Screen name="Home" component={HomeStack} />
          <Tab.Screen name="Servers" component={ServerListScreen} />
          <Tab.Screen name="Profile">
            {props => <ProfileScreen {...props} onLogout={() => setIsLoggedIn(false)} />}
          </Tab.Screen>
        </Tab.Navigator>
      </NavigationContainer>
    </SafeAreaProvider>
  );
}

