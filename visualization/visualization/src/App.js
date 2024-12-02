import React from 'react';
import Breadboard from './components/Breadboard';
import Button from './components/Button';
import './App.css';

const App = () => {
  return (
    <div className='app'>
      <Breadboard />
      <Button onClick={() => console.log('button pressed')} />
    </div>
  )
}

export default App;